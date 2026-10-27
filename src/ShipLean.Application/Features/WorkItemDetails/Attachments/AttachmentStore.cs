using ShipLean.Application.Storage;
using Dapper;
using Microsoft.Net.Http.Headers;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// IAttachmentStore over Postgres rows and IBlobStore bytes. Both uploads and imports count bytes while streaming into the
// blob store and refuse a file over AttachmentLimits (nothing is kept). Archiving sets archived_at; the row and its blob stay.
// Uploading a name already on the item archives the old row (the new one counts it in ReplacedCount); the item holds at most
// AttachmentLimits.MaxItemBytes live (both checked under a lock on the item row, so concurrent uploads cannot overshoot either), and an SVG is stored sanitised and marked so. A re-run import writes a new blob and swaps the row to it after the put succeeds. Add and archive publish
// attachmentsChanged after the write; an import publishes nothing (it reports through importProgress).
public sealed class AttachmentStore(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    IBlobStore blobs,
    TimeProvider clock,
    IProjectEvents events,
    AttachmentLimits limits) : IAttachmentStore
{
    private const string FallbackContentType = "application/octet-stream";

    private const string SvgContentType = "image/svg+xml";

    private static readonly string SelectLive = $"SELECT {AttachmentRow.Columns} {AttachmentRow.From} WHERE a.id = @id AND a.archived_at IS NULL";

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireWorkItemAsync(connection, workItemId, cancellationToken);
        var rows = await connection.QueryAsync<AttachmentRow>(new CommandDefinition(
            $"SELECT {AttachmentRow.Columns} {AttachmentRow.From} WHERE a.work_item_id = @workItemId AND a.archived_at IS NULL ORDER BY a.created_at, a.id",
            new { workItemId }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToDto(clock))];
    }

    public async Task<AttachmentDto> AddAsync(Guid workItemId, string name, string contentType, Stream content, AttachmentSource? source = null, CancellationToken cancellationToken = default)
    {
        var cleanName = RequireName(name);
        var type = CleanContentType(contentType);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireWorkItemAsync(connection, workItemId, cancellationToken);
        var id = Guid.NewGuid();
        var storageRef = NewRef();
        var (size, storedType, sanitized) = await PutAsync(storageRef, content, cleanName, type, cancellationToken);
        type = storedType;
        var searchText = AttachmentKinds.Of(cleanName, type) == AttachmentKind.Markdown && size <= MaxSearchTextBytes
            ? await ReadTextAsync(storageRef, cancellationToken)
            : null;
        var row = new NewAttachment(id, workItemId, cleanName, type, size, storageRef, sanitized, searchText, source);
        AttachmentRow added;
        try
        {
            added = await StoreAsync(connection, row, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // The item lock should leave no second live row to collide with; if one slipped through, it is replaced like any other.
            added = await StoreAsync(connection, row, cancellationToken);
        }

        await PublishChangedAsync(connection, workItemId, cancellationToken);
        return added.ToDto(clock);
    }

    public async Task<(AttachmentDto Attachment, BlobContent Content)> OpenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (attachment, content, _) = await OpenServedAsync(id, cancellationToken);
        return (attachment, content);
    }

    // OpenAsync plus whether a browser may show the file in place (AttachmentServing). The caller disposes the content.
    public async Task<(AttachmentDto Attachment, BlobContent Content, bool Inline)> OpenServedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AttachmentRow>(new CommandDefinition(SelectLive, new { id }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("That attachment does not exist.");
        var blob = await blobs.GetAsync(row.StorageRef, cancellationToken) ?? throw ApiException.NotFound("That attachment does not exist.");
        return (row.ToDto(clock), new BlobContent(blob.Content, row.ContentType), AttachmentServing.IsInline(row.ContentType, row.Sanitized));
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var workItemId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "UPDATE attachment SET archived_at = @now WHERE id = @id AND archived_at IS NULL RETURNING work_item_id",
            new { id, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
        if (workItemId is null)
        {
            throw ApiException.NotFound("That attachment does not exist.");
        }

        await PublishChangedAsync(connection, workItemId.Value, cancellationToken);
    }

    public async Task<Guid> ImportAsync(Guid workItemId, ImportedAttachment attachment, Stream content, CancellationToken cancellationToken = default)
    {
        var cleanName = RequireName(attachment.Name);
        var type = CleanContentType(attachment.ContentType);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireWorkItemAsync(connection, workItemId, cancellationToken);
        if (attachment.UploadedById is { } uploader
            && !await connection.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS (SELECT 1 FROM app_user WHERE id = @uploader)", new { uploader }, cancellationToken: cancellationToken)))
        {
            throw ApiException.NotFound("That user does not exist.");
        }

        var storageRef = NewRef();
        var (size, storedType, sanitized) = await PutAsync(storageRef, content, cleanName, type, cancellationToken);
        type = storedType;
        // Under the item lock, like an upload: another live file of the same name is archived (as a replace would), so the live-name index holds.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT id FROM work_item WHERE id = @workItemId FOR NO KEY UPDATE", new { workItemId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE attachment SET archived_at = @now
            WHERE work_item_id = @workItemId AND name = @name AND archived_at IS NULL
              AND (original_system, original_system_id) IS DISTINCT FROM (@system, @systemId)
            """,
            new { workItemId, name = cleanName, now = clock.GetUtcNow(), system = attachment.OriginalSystem, systemId = attachment.OriginalSystemId },
            transaction, cancellationToken: cancellationToken));
        var id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            """
            INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref, uploaded_by, original_system, original_system_id, created_at, sanitized)
            VALUES (@id, @workItemId, @name, @type, @size, @storageRef, @uploader, @system, @systemId, @createdAt, @sanitized)
            ON CONFLICT (work_item_id, original_system, original_system_id) WHERE original_system IS NOT NULL
            DO UPDATE SET name = excluded.name, content_type = excluded.content_type, size_bytes = excluded.size_bytes, storage_ref = excluded.storage_ref, sanitized = excluded.sanitized
            RETURNING id
            """,
            new
            {
                id = Guid.NewGuid(), workItemId, name = cleanName, type, size, storageRef, uploader = attachment.UploadedById,
                system = attachment.OriginalSystem, systemId = attachment.OriginalSystemId, createdAt = attachment.CreatedAt, sanitized,
            },
            transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    // One transaction per item at a time: the item row is locked, so the cap check and the archive-then-insert see each other's commits.
    private async Task<AttachmentRow> StoreAsync(NpgsqlConnection connection, NewAttachment row, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT id FROM work_item WHERE id = @workItemId FOR NO KEY UPDATE", new { row.WorkItemId }, transaction, cancellationToken: cancellationToken));
        var liveElsewhere = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT coalesce(sum(size_bytes), 0)::bigint FROM attachment WHERE work_item_id = @workItemId AND archived_at IS NULL AND name <> @name",
            new { row.WorkItemId, row.Name }, transaction, cancellationToken: cancellationToken));
        if (liveElsewhere + row.Size > limits.MaxItemBytes)
        {
            throw ApiException.Validation(limits.ItemFullMessage);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE attachment SET archived_at = @now WHERE work_item_id = @workItemId AND name = @name AND archived_at IS NULL",
            new { row.WorkItemId, row.Name, now = clock.GetUtcNow() }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref, uploaded_by, created_at, search_text, agent_name, uploaded_via, sanitized)
            VALUES (@id, @workItemId, @name, @type, @size, @storageRef, @uploader, @now, @searchText, @agentName, @via, @sanitized)
            """,
            new
            {
                row.Id, row.WorkItemId, row.Name, type = row.ContentType, size = row.Size, row.StorageRef, uploader = currentUser.Id, now = clock.GetUtcNow(),
                row.SearchText, agentName = row.Source?.AgentName, via = row.Source?.Via, row.Sanitized,
            },
            transaction, cancellationToken: cancellationToken));
        // Read before the commit releases the lock: afterwards a racing upload of the same name could already have replaced this row.
        var stored = await connection.QuerySingleAsync<AttachmentRow>(new CommandDefinition(SelectLive, new { row.Id }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return stored;
    }

    private sealed record NewAttachment(Guid Id, Guid WorkItemId, string Name, string ContentType, long Size, string StorageRef, bool Sanitized, string? SearchText, AttachmentSource? Source);

    // A Markdown file over this size is stored but not searchable by its text.
    private const long MaxSearchTextBytes = 1024 * 1024;

    private const int MaxNameLength = 255;

    private const int MaxContentTypeLength = 255;

    private static string NewRef() => $"attachments/{Guid.NewGuid():N}";

    // A content type the browser would not accept becomes a download of unknown type, never an error or a header the response
    // cannot carry. Only the media type and a charset are kept, and a result over MaxContentTypeLength falls back too.
    private static string CleanContentType(string? contentType)
    {
        if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed) || !parsed.MediaType.HasValue)
        {
            return FallbackContentType;
        }

        var clean = parsed.Charset.HasValue ? $"{parsed.MediaType.Value}; charset={parsed.Charset.Value}" : parsed.MediaType.Value;
        return clean.Length > MaxContentTypeLength || clean.Any(char.IsControl) ? FallbackContentType : clean;
    }

    private static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw ApiException.Validation("An attachment needs a name.");
        }

        var trimmed = AttachmentNames.Clean(name);
        if (trimmed.Length == 0)
        {
            throw ApiException.Validation("An attachment needs a name.");
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw ApiException.Validation($"An attachment name is at most {MaxNameLength} characters.");
        }

        if (trimmed.Any(char.IsControl))
        {
            throw ApiException.Validation("An attachment name cannot contain control characters.");
        }

        if (trimmed.Any(IsBidiControl))
        {
            throw ApiException.Validation("An attachment name cannot contain direction-control characters.");
        }

        return trimmed;
    }

    // The embedding, override and isolate controls: they make a name display as something other than what is stored.
    private static bool IsBidiControl(char c) => c is >= '\u202A' and <= '\u202E' or >= '\u2066' and <= '\u2069';

    // The text of a stored file for search. Postgres text cannot hold NUL, so those are dropped.
    private async Task<string?> ReadTextAsync(string storageRef, CancellationToken cancellationToken)
    {
        var blob = await blobs.GetAsync(storageRef, cancellationToken);
        if (blob is null)
        {
            return null;
        }

        await using (blob.Content)
        {
            using var reader = new StreamReader(blob.Content);
            return (await reader.ReadToEndAsync(cancellationToken)).Replace("\0", string.Empty, StringComparison.Ordinal);
        }
    }

    // An SVG is read whole (it is within the file limit), cleaned, and the cleaned bytes are stored as image/svg+xml whatever
    // type was declared. Bytes that are not an SVG document are kept as they came but as an opaque download type, never an image.
    private async Task<(long Size, string ContentType, bool Sanitized)> PutAsync(string storageRef, Stream content, string name, string contentType, CancellationToken cancellationToken)
    {
        await using var limited = new CountingLimitStream(content, limits.MaxBytes);
        if (!IsSvg(name, contentType))
        {
            return (await blobs.PutAsync(storageRef, limited, contentType, cancellationToken), contentType, false);
        }

        using var buffer = new MemoryStream();
        await limited.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        if (!SvgSanitizer.TrySanitize(buffer, out var clean))
        {
            buffer.Position = 0;
            return (await blobs.PutAsync(storageRef, buffer, FallbackContentType, cancellationToken), FallbackContentType, false);
        }

        await using var cleanStream = new MemoryStream(clean);
        return (await blobs.PutAsync(storageRef, cleanStream, SvgContentType, cancellationToken), SvgContentType, true);
    }

    private static bool IsSvg(string name, string contentType)
        => Path.GetExtension(name).Equals(".svg", StringComparison.OrdinalIgnoreCase) || contentType.StartsWith(SvgContentType, StringComparison.OrdinalIgnoreCase);

    private async Task PublishChangedAsync(NpgsqlConnection connection, Guid workItemId, CancellationToken cancellationToken)
    {
        var projectId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "SELECT project_id FROM work_item WHERE id = @workItemId", new { workItemId }, cancellationToken: cancellationToken));
        await events.PublishAsync(new AttachmentsChanged(projectId, workItemId));
    }

    private static async Task RequireWorkItemAsync(NpgsqlConnection connection, Guid workItemId, CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM work_item WHERE id = @workItemId)", new { workItemId }, cancellationToken: cancellationToken));
        if (!exists)
        {
            throw ApiException.NotFound("That work item does not exist.");
        }
    }
}
