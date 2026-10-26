using System.Text.Json;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Application.Markdown;
using ShipLean.Application.Sprints;
using ShipLean.Application.WorkItems.History;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItems;

// The work item read model by SQL: WorkItemDto with its refs, rendered markdown, children, links and labels. Callers check
// IProjectAccess first. The store reuses it (inside its transaction) to build the conflict body.
[RegisterPerRequest]
public sealed class WorkItemReads(NpgsqlDataSource dataSource, MarkdownRenderer renderer, TimeProvider time)
{
    public async Task<WorkItemDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await WorkItemQueries.RowsAsync(connection, null, $"{WorkItemRow.Select} WHERE w.id = @id", new { id }, cancellationToken);
        return (await BuildAsync(connection, null, rows, cancellationToken)).SingleOrDefault() ?? throw ApiException.NotFound("Work item not found.");
    }

    // The key is matched exactly (keys are stored upper case); an unknown key and another project's key are both not found.
    public async Task<WorkItemDto> GetByKeyAsync(Guid projectId, string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await WorkItemQueries.RowsAsync(connection, null, $"{WorkItemRow.Select} WHERE w.project_id = @projectId AND w.key = @key", new { projectId, key = key.ToUpperInvariant() }, cancellationToken);
        return (await BuildAsync(connection, null, rows, cancellationToken)).SingleOrDefault() ?? throw ApiException.NotFound("Work item not found.");
    }

    // Backlog order; items in the Removed category are left out.
    public async Task<IReadOnlyList<WorkItemDto>> ListAsync(Guid projectId, WorkItemTypeId? type, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await WorkItemQueries.RowsAsync(
            connection,
            null,
            $"""
            {WorkItemRow.Select}
            JOIN state s ON s.id = w.state_id
            WHERE w.project_id = @projectId AND s.category <> 'removed' AND (@type::text IS NULL OR w.type = @type)
            ORDER BY w.backlog_order, w.number
            """,
            new { projectId, type = type is { } t ? WorkItemTypes.Db(t) : null },
            cancellationToken);
        return await BuildAsync(connection, null, rows, cancellationToken);
    }

    // The 409 body for a stale expectedRevision, read inside the writer's transaction.
    public async Task<ConflictDto> ConflictAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, int expectedRevision, CancellationToken cancellationToken)
    {
        var current = (await WorkItemQueries.RowsAsync(connection, transaction, $"{WorkItemRow.Select} WHERE w.id = @id", new { id }, cancellationToken)).Single();
        var item = (await BuildAsync(connection, transaction, [current], cancellationToken)).Single();
        var theirs = (await WorkItemQueries.RevisionRowsAsync(connection, transaction, current.Id, cancellationToken)).Where(r => r.Rev > expectedRevision).Reverse().ToList();
        var users = await UsersAsync(connection, transaction, [.. theirs.Select(r => r.ChangedBy).OfType<Guid>().Distinct()], cancellationToken);
        var dtos = theirs.Select(r => new RevisionDto(
            r.Rev,
            r.ChangedBy is { } by ? users.GetValueOrDefault(by) : null,
            r.ChangedAt,
            DisplayDate.Relative(r.ChangedAt, time),
            r.Origin,
            r.Changes,
            r.AgentName,
            r.Webhook)).ToList();
        return new ConflictDto("This work item was changed by someone else.", ApiErrorCodes.Conflict, item, dtos);
    }

    private async Task<IReadOnlyList<WorkItemDto>> BuildAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, IReadOnlyList<WorkItemRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.Id).ToArray();
        var children = (await connection.QueryAsync<ChildRow>(new CommandDefinition(
            """
            SELECT c.parent_id AS parentId, c.id, c.key, c.type, c.title, c.state_id AS stateId, c.assigned_to AS assigneeId, c.story_points AS points
            FROM work_item c
            WHERE c.parent_id = ANY(@ids)
            ORDER BY c.backlog_order, c.number
            """,
            new { ids }, transaction, cancellationToken: cancellationToken))).ToList();
        var links = await WorkItemQueries.LinkRowsAsync(connection, transaction, ids, cancellationToken);
        // The live image attachments the descriptions' images are matched against, oldest first (the newest of a name wins).
        var images = (await connection.QueryAsync<AttachmentRow>(new CommandDefinition(
            $"""
            SELECT a.id, a.work_item_id AS workItemId, a.name, a.content_type AS contentType, a.size_bytes AS sizeBytes, a.created_at AS createdAt,
                   u.id AS uploaderId, u.name AS uploaderName, u.initials AS uploaderInitials, u.avatar_colour AS uploaderColour, {UserRefs.PhotoUrlSql} AS uploaderPhotoUrl,
                   a.agent_name AS agentName, a.uploaded_via AS uploadedVia,
                   (SELECT count(*)::int FROM attachment r WHERE r.work_item_id = a.work_item_id AND r.name = a.name AND r.archived_at IS NOT NULL) AS replacedCount
            FROM attachment a LEFT JOIN app_user u ON u.id = a.uploaded_by
            WHERE a.work_item_id = ANY(@ids) AND a.archived_at IS NULL
            ORDER BY a.created_at, a.id
            """,
            new { ids }, transaction, cancellationToken: cancellationToken)))
            .Select(a => a.ToDto(time)).Where(a => a.Kind == AttachmentKind.Image).ToList();
        var users = await UsersAsync(
            connection, transaction,
            [.. rows.SelectMany(r => new[] { r.AssigneeId, r.CreatedBy, r.ChangedBy, r.StatusVerifiedBy }).Concat(children.Select(c => c.AssigneeId)).OfType<Guid>().Distinct()],
            cancellationToken);
        var states = (await connection.QueryAsync<StateRow>(new CommandDefinition(
            "SELECT id, name, category FROM state WHERE id = ANY(@ids)",
            new { ids = rows.Select(r => r.StateId).Concat(children.Select(c => c.StateId)).Distinct().ToArray() }, transaction, cancellationToken: cancellationToken)))
            .ToDictionary(s => s.Id, s => new StateRef(s.Id, s.Name, Enum.Parse<StateCategory>(s.Category, ignoreCase: true)));
        var today = SprintLabels.Today(time);
        var sprints = (await connection.QueryAsync<SprintRow>(new CommandDefinition(
            "SELECT id, name, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate FROM sprint WHERE id = ANY(@ids)",
            new { ids = rows.Select(r => r.SprintId).OfType<Guid>().Distinct().ToArray() }, transaction, cancellationToken: cancellationToken)))
            .ToDictionary(s => s.Id, s => s.ToRef(today));
        var parents = (await connection.QueryAsync<ParentRow>(new CommandDefinition(
            "SELECT id, key, type, title FROM work_item WHERE id = ANY(@ids)",
            new { ids = rows.Select(r => r.ParentId).OfType<Guid>().Distinct().ToArray() }, transaction, cancellationToken: cancellationToken)))
            .ToDictionary(p => p.Id, p => new ParentRefDto(p.Id, p.Key, WorkItemTypes.Ref(WorkItemTypes.Parse(p.Type)), p.Title));

        return [.. rows.Select(row => ToDto(row, users, states, sprints, parents, children, links, images))];
    }

    private WorkItemDto ToDto(
        WorkItemRow row,
        IReadOnlyDictionary<Guid, UserRef> users,
        IReadOnlyDictionary<Guid, StateRef> states,
        IReadOnlyDictionary<Guid, SprintRef> sprints,
        IReadOnlyDictionary<Guid, ParentRefDto> parents,
        IReadOnlyList<ChildRow> children,
        IReadOnlyList<WorkItemLinkRow> links,
        IReadOnlyList<AttachmentDto> images)
    {
        var itemImages = new MarkdownImageContext([.. images.Where(a => a.WorkItemId == row.Id)]);
        var description = renderer.Render(row.Description, itemImages);
        var repro = renderer.Render(row.ReproSteps, itemImages);
        return new WorkItemDto(
            row.Id,
            row.ProjectId,
            row.Key,
            row.Number,
            WorkItemTypes.Ref(WorkItemTypes.Parse(row.Type)),
            row.Title,
            description.Markdown,
            description.Html,
            row.Criteria,
            repro.Markdown,
            repro.Html,
            row.Severity,
            states[row.StateId],
            Lookup(users, row.AssigneeId),
            Lookup(users, row.CreatedBy),
            Lookup(users, row.ChangedBy),
            row.SprintId is { } sprint ? sprints[sprint] : null,
            row.Priority,
            WorkItemPoints.Normalise(row.Points),
            row.ParentId is { } parent ? parents[parent] : null,
            row.Tags,
            new BoardPositionDto(row.ColumnId, row.ColumnDone, row.LaneId, row.ColumnOrder),
            row.BacklogOrder,
            [.. links.Where(l => l.SourceId == row.Id).Select(l => l.ToDto(time))],
            [.. children.Where(c => c.ParentId == row.Id).Select(c => new ChildSummaryDto(
                c.Id, c.Key, WorkItemTypes.Ref(WorkItemTypes.Parse(c.Type)), c.Title, states[c.StateId], Lookup(users, c.AssigneeId), WorkItemPoints.Normalise(c.Points)))],
            row.Original,
            row.Revision,
            row.CreatedAt,
            DisplayDate.Relative(row.CreatedAt, time),
            row.ChangedAt,
            DisplayDate.Relative(row.ChangedAt, time),
            row.Goal,
            row.StatusLine,
            row.StatusVerifiedAt is { } verified
                ? new StatusVerifiedDto(
                    verified, DisplayDate.Relative(verified, time), StatusVerifiedDto.IsStale(verified, time.GetUtcNow()),
                    Lookup(users, row.StatusVerifiedBy), row.StatusVerifiedAgent, row.StatusVerifiedVia)
                : null,
            row.Slug,
            row.StartedOn,
            row.StartedOn is { } started ? DisplayDate.ShortDay(DisplayDate.StartOfDay(started, time.LocalTimeZone), time.GetUtcNow(), time.LocalTimeZone) : null);
    }

    private static UserRef? Lookup(IReadOnlyDictionary<Guid, UserRef> users, Guid? id) => id is { } value ? users.GetValueOrDefault(value) : null;

    private static async Task<IReadOnlyDictionary<Guid, UserRef>> UsersAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid[] ids, CancellationToken cancellationToken)
        => (await connection.QueryAsync<UserRef>(new CommandDefinition(
            $"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = ANY(@ids)", new { ids }, transaction, cancellationToken: cancellationToken)))
            .ToDictionary(u => u.Id);

    private sealed record ChildRow(Guid ParentId, Guid Id, string Key, string Type, string Title, Guid StateId, Guid? AssigneeId, decimal? Points);

    // An attachment as the image matching reads it. The attachment read model lives in Features/WorkItemDetails, which this
    // feature can't reference, so the kind rule below repeats its AttachmentKinds.
    private sealed record AttachmentRow(
        Guid Id,
        Guid WorkItemId,
        string Name,
        string ContentType,
        long SizeBytes,
        DateTimeOffset CreatedAt,
        Guid? UploaderId,
        string? UploaderName,
        string? UploaderInitials,
        string? UploaderColour,
        string? UploaderPhotoUrl,
        string? AgentName,
        string? UploadedVia,
        int ReplacedCount)
    {
        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg"];

        private static readonly string[] ImageTypes = ["image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml"];

        public AttachmentDto ToDto(TimeProvider clock)
            => new(
                Id, WorkItemId, Name, ContentType, SizeBytes,
                UploaderId is { } id ? new UserRef(id, UploaderName!, UploaderInitials!, UploaderColour!, UploaderPhotoUrl) : null,
                CreatedAt, DisplayDate.Relative(CreatedAt, clock), $"/api/attachments/{Id}/content", Kind(), ReplacedCount, AgentName, UploadedVia);

        // Markdown by .md or text/markdown first, then the image extensions and types, as AttachmentKinds.Of decides.
        private AttachmentKind Kind()
        {
            var extension = Path.GetExtension(Name);
            var mediaType = ContentType.Split(';')[0].Trim();
            if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase) || mediaType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase))
            {
                return AttachmentKind.Markdown;
            }

            return ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) || ImageTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase)
                ? AttachmentKind.Image
                : AttachmentKind.File;
        }
    }

    private sealed record StateRow(Guid Id, string Name, string Category);

    private sealed record ParentRow(Guid Id, string Key, string Type, string Title);

    private sealed record SprintRow(Guid Id, string Name, string StartDate, string EndDate)
    {
        public SprintRef ToRef(DateOnly today)
        {
            var start = DateOnly.ParseExact(StartDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var end = DateOnly.ParseExact(EndDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            return new SprintRef(Id, Name, SprintLabels.Phase(start, end, today).ToStatus());
        }
    }
}
