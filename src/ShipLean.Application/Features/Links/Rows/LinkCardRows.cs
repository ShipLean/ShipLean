using Dapper;
using Npgsql;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Links.Rows;

// Reads and writes work_item_link rows for the link endpoints, the card service, the refresh and the reported state. Plain
// storage: no access check, no validation, no events (the caller does those). Nothing is hard-deleted: Archive sets archived_at,
// and every read skips archived rows. A write to an unknown or archived link changes nothing and answers false.
public sealed class LinkCardRows(NpgsqlDataSource dataSource)
{
    private sealed record ArchivedRow(Guid WorkItemId, Guid ProjectId);

    private sealed record WorkItemLinkQueryRow(Guid Id, string TypeText, Guid WorkItemId, string Key, string Title, string ItemType, Guid StateId, string StateName, string StateCategory);

    // The item's live URL links, oldest first.
    public async Task<IReadOnlyList<LinkCardRow>> ListAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<LinkCardRow>(new CommandDefinition(
            $"SELECT {LinkCardRow.Columns} {LinkCardRow.From} WHERE l.source_id = @workItemId AND l.link_type = 'url' AND l.archived_at IS NULL ORDER BY l.created_at, l.id",
            new { workItemId }, cancellationToken: cancellationToken))];
    }

    // The item's live links to other work items, oldest first.
    public async Task<IReadOnlyList<WorkItemLinkRowDto>> ListWorkItemLinksAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<WorkItemLinkQueryRow>(new CommandDefinition(
            $"{WorkItemLinkSelect} WHERE l.source_id = @workItemId AND l.link_type <> 'url' AND l.archived_at IS NULL ORDER BY l.created_at, l.id",
            new { workItemId }, cancellationToken: cancellationToken));
        return [.. rows.Select(ToDto)];
    }

    // One live link to another work item, as the work item list shows it. Null when it is archived, unknown or a URL link.
    public async Task<WorkItemLinkRowDto?> FindWorkItemLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<WorkItemLinkQueryRow>(new CommandDefinition(
            $"{WorkItemLinkSelect} WHERE l.id = @id AND l.link_type <> 'url' AND l.archived_at IS NULL", new { id }, cancellationToken: cancellationToken));
        return row is null ? null : ToDto(row);
    }

    // One live URL link. Null when it is archived, unknown or a link to a work item.
    public async Task<LinkCardRow?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LinkCardRow>(new CommandDefinition(
            $"SELECT {LinkCardRow.Columns} {LinkCardRow.From} WHERE l.id = @id AND l.link_type = 'url' AND l.archived_at IS NULL", new { id }, cancellationToken: cancellationToken));
    }

    // Every live URL link with exactly this address, on any work item of a project the user is a live member of.
    public async Task<IReadOnlyList<LinkCardRow>> FindByUrlAsync(string url, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<LinkCardRow>(new CommandDefinition(
            $"""
            SELECT {LinkCardRow.Columns} {LinkCardRow.From}
            JOIN project_member m ON m.project_id = w.project_id AND m.user_id = @userId AND m.archived_at IS NULL
            WHERE l.url = @url AND l.link_type = 'url' AND l.archived_at IS NULL
            ORDER BY l.created_at, l.id
            """,
            new { url, userId }, cancellationToken: cancellationToken))];
    }

    // A blank title or note is stored as null. Returns the new row's id.
    public async Task<Guid> InsertUrlAsync(
        Guid workItemId, string url, string? title, string? note, LinkKind kind, Guid? createdBy, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO work_item_link (id, source_id, link_type, url, title, note, kind, created_by, created_at)
            VALUES (@id, @workItemId, 'url', @url, @title, @note, @kind, @createdBy, @createdAt)
            """,
            new { id, workItemId, url, title = NullIfBlank(title), note = NullIfBlank(note), kind = LinkCardRow.ToDb(kind), createdBy, createdAt }, cancellationToken: cancellationToken));
        return id;
    }

    // Returns the new row's id. The database refuses a link to the item itself and a Url type; the caller checks first.
    public async Task<Guid> InsertWorkItemLinkAsync(Guid sourceId, Guid targetId, LinkType type, Guid? createdBy, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO work_item_link (id, source_id, target_id, link_type, created_by, created_at)
            VALUES (@id, @sourceId, @targetId, @type, @createdBy, @createdAt)
            """,
            new { id, sourceId, targetId, type = LinkCardRow.ToDb(type), createdBy, createdAt }, cancellationToken: cancellationToken));
        return id;
    }

    // Replaces everything the preview holds (null clears a field). Does not touch the typed title, note or state.
    public async Task<bool> UpdatePreviewAsync(Guid id, LinkPreviewWrite preview, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE work_item_link
            SET preview_title = @title, preview_description = @description, preview_site = @site, preview_image_key = @imageKey,
                preview_favicon_key = @faviconKey, preview_status = @status, preview_fetched_at = @fetchedAt,
                number = @number, location = @location, author = @author
            WHERE id = @id AND link_type = 'url' AND archived_at IS NULL
            """,
            new
            {
                id, title = preview.Title, description = preview.Description, site = preview.Site, imageKey = preview.ImageKey, faviconKey = preview.FaviconKey,
                status = LinkCardRow.ToDb(preview.Status), fetchedAt = preview.FetchedAt, number = preview.Number, location = preview.Location, author = preview.Author,
            }, cancellationToken: cancellationToken)) > 0;
    }

    // Replaces the state, where it came from, when it was true and who reported it.
    public async Task<bool> UpdateStateAsync(Guid id, LinkStateWrite state, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE work_item_link
            SET state = @state, state_source = @source, state_as_of = @asOf, state_reported_by = @reportedBy
            WHERE id = @id AND link_type = 'url' AND archived_at IS NULL
            """,
            new { id, state = state.State is { } s ? LinkCardRow.ToDb(s) : null, source = LinkCardRow.ToDb(state.Source), asOf = state.AsOf, reportedBy = state.ReportedBy },
            cancellationToken: cancellationToken)) > 0;
    }

    // Writes both the typed title and the note as given; blank is stored as null.
    public async Task<bool> UpdateTextAsync(Guid id, string? title, string? note, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE work_item_link SET title = @title, note = @note WHERE id = @id AND link_type = 'url' AND archived_at IS NULL",
            new { id, title = NullIfBlank(title), note = NullIfBlank(note) }, cancellationToken: cancellationToken)) > 0;
    }

    // Hides a URL link or a link to a work item. Returns the work item it was on and that item's project, or null when it was unknown or already archived.
    public async Task<(Guid WorkItemId, Guid ProjectId)?> ArchiveAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ArchivedRow>(new CommandDefinition(
            """
            UPDATE work_item_link l SET archived_at = @at
            FROM work_item w
            WHERE l.id = @id AND l.archived_at IS NULL AND w.id = l.source_id
            RETURNING l.source_id AS WorkItemId, w.project_id AS ProjectId
            """,
            new { id, at }, cancellationToken: cancellationToken));
        return row is null ? null : (row.WorkItemId, row.ProjectId);
    }

    // The project of a work item, or null for an unknown one.
    public async Task<Guid?> ProjectOfAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT project_id FROM work_item WHERE id = @workItemId", new { workItemId }, cancellationToken: cancellationToken));
    }

    // The id of the work item with this key in the project, or null. Keys are upper case (SHIP-142); the lookup ignores case.
    public async Task<Guid?> FindWorkItemByKeyAsync(Guid projectId, string key, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM work_item WHERE project_id = @projectId AND key = upper(@key)", new { projectId, key = key.Trim() }, cancellationToken: cancellationToken));
    }

    private const string WorkItemLinkSelect =
        """
        SELECT l.id, l.link_type AS typeText, t.id AS workItemId, t.key, t.title, t.type AS itemType,
               s.id AS stateId, s.name AS stateName, s.category AS stateCategory
        FROM work_item_link l
        JOIN work_item t ON t.id = l.target_id
        JOIN state s ON s.id = t.state_id
        """;

    private static WorkItemLinkRowDto ToDto(WorkItemLinkQueryRow row)
    {
        var type = Enum.Parse<WorkItemTypeId>(row.ItemType, ignoreCase: true);
        return new WorkItemLinkRowDto(
            row.Id, Enum.Parse<LinkType>(row.TypeText, ignoreCase: true), row.WorkItemId, row.Key, row.Title,
            new TypeRef(type, type == WorkItemTypeId.Story ? "User Story" : type.ToString()),
            new StateRef(row.StateId, row.StateName, Enum.Parse<StateCategory>(row.StateCategory, ignoreCase: true)));
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
