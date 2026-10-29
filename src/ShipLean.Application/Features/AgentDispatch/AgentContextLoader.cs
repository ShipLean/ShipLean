using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch.Rendering;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.AgentDispatch;

// Maps an item's rows onto the renderer's plain input. A comment's author is a name only (no email leaves for the receiver; the creator's does,
// for the relay's allow-list). Reads only; the session id and creation time are the caller's.
// Markdown attachments are read through the blob store, in upload order, up to InlineAttachmentBytes in all (UTF-8 bytes); the first one that does
// not fit, and every attachment after it, is left as a link. Without a blob store (a follow-up prompt, which sends no promptContext) all are links.
internal sealed class AgentContextLoader(NpgsqlDataSource dataSource, IBlobStore? blobs = null)
{
    public const int InlineAttachmentBytes = 200 * 1024;

    public async Task<AgentContextInput> LoadAsync(
        Guid workItemId, Guid sessionId, DateTimeOffset createdAt, Guid connectionId, Guid creatorId, AgentRepositoryInput repository, string? fallbackBaseUrl, string? guidance, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var item = await connection.QuerySingleOrDefaultAsync<AgentItemRow>(new CommandDefinition(
            """
            SELECT w.id, w.key, w.title, w.description, w.acceptance_criteria::text AS criteria, to_jsonb(w.tags)::text AS tags, p.id AS projectId, p.key AS projectKey, p.name AS projectName,
                   w.type, s.name AS state, au.name AS assignee, sp.name AS sprint, w.story_points AS points
            FROM work_item w JOIN project p ON p.id = w.project_id JOIN state s ON s.id = w.state_id
            LEFT JOIN app_user au ON au.id = w.assigned_to LEFT JOIN sprint sp ON sp.id = w.sprint_id
            WHERE w.id = @workItemId
            """,
            new { workItemId }, cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("That work item was not found.");
        var creator = await connection.QuerySingleAsync<AgentUserInput>(new CommandDefinition(
            "SELECT id::text AS id, name, email FROM app_user WHERE id = @creatorId", new { creatorId }, cancellationToken: cancellationToken));
        var ancestors = (await connection.QueryAsync<AgentRelatedRow>(new CommandDefinition(
            """
            WITH RECURSIVE up AS (
                SELECT w.id, w.key, w.type, w.title, w.description, w.parent_id, 1 AS depth
                FROM work_item w WHERE w.id = (SELECT parent_id FROM work_item WHERE id = @workItemId)
                UNION ALL
                SELECT w.id, w.key, w.type, w.title, w.description, w.parent_id, up.depth + 1
                FROM work_item w JOIN up ON w.id = up.parent_id)
            SELECT id, key, type, title, description FROM up ORDER BY depth
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var comments = (await connection.QueryAsync<AgentCommentRow>(new CommandDefinition(
            """
            SELECT c.id, c.author_id AS authorId, coalesce(u.name, '') AS authorName, c.created_at AS createdAt, c.body
            FROM comment c LEFT JOIN app_user u ON u.id = c.author_id
            WHERE c.work_item_id = @workItemId AND c.archived_at IS NULL
            ORDER BY c.created_at, c.id
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var children = (await connection.QueryAsync<AgentChildRow>(new CommandDefinition(
            """
            SELECT c.key, c.title, s.name AS state
            FROM work_item c JOIN state s ON s.id = c.state_id
            WHERE c.parent_id = @workItemId
            ORDER BY c.number, c.id
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var links = (await connection.QueryAsync<AgentLinkRow>(new CommandDefinition(
            """
            SELECT l.link_type AS type, t.key AS targetKey, tp.key AS targetProjectKey, t.title AS targetTitle, l.url, l.title
            FROM work_item_link l LEFT JOIN work_item t ON t.id = l.target_id LEFT JOIN project tp ON tp.id = t.project_id
            WHERE l.source_id = @workItemId AND l.archived_at IS NULL
            ORDER BY l.created_at, l.id
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var attachmentRows = (await connection.QueryAsync<AgentAttachmentRow>(new CommandDefinition(
            """
            SELECT id, name, content_type AS contentType, size_bytes AS sizeBytes, storage_ref AS storageRef
            FROM attachment WHERE work_item_id = @workItemId AND archived_at IS NULL
            ORDER BY created_at, id
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var baseUrl = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT base_url FROM instance_settings", cancellationToken: cancellationToken));
        baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? fallbackBaseUrl ?? string.Empty : baseUrl;

        string Url(string key) => McpOpenUrls.WorkItem(baseUrl, item.ProjectKey, key);
        AgentRelatedItemInput Related(AgentRelatedRow row) => new(row.Id.ToString(), row.Key, row.Title, Blank(row.Description), Url(row.Key));
        var attachments = await AttachmentsAsync(attachmentRows, row => McpOpenUrls.WorkItemAttachment(baseUrl, item.ProjectKey, item.Key, row.Id), cancellationToken);
        var criteria = JsonSerializer.Deserialize<List<ChecklistItem>>(item.Criteria, PersistedJson.Options) ?? [];
        var parent = ancestors.FirstOrDefault();
        var feature = ancestors.FirstOrDefault(a => a.Type == "feature");
        return new AgentContextInput(
            sessionId.ToString(),
            connectionId.ToString(),
            createdAt,
            creator,
            new AgentProjectInput(item.ProjectId.ToString(), item.ProjectKey, item.ProjectName),
            repository,
            new AgentItemInput(
                item.Id.ToString(), item.Key, item.Title, Blank(item.Description), Url(item.Key),
                JsonSerializer.Deserialize<string[]>(item.Tags, PersistedJson.Options) ?? [],
                [.. criteria.Select(c => new AgentCriterionInput(c.Text, c.Done))],
                item.Type, item.State, item.Assignee, item.Sprint, item.Points,
                [.. links.Select(l => l.TargetKey is null
                    ? new AgentLinkInput(l.Type, null, l.Title, l.Url ?? string.Empty)
                    : new AgentLinkInput(l.Type, l.TargetKey, l.TargetTitle, McpOpenUrls.WorkItem(baseUrl, l.TargetProjectKey ?? item.ProjectKey, l.TargetKey)))],
                [.. children.Select(c => new AgentChildInput(c.Key, c.Title, c.State, Url(c.Key)))],
                attachments),
            parent is null ? null : Related(parent),
            feature is null ? null : Related(feature),
            [.. comments.Select(c => new AgentCommentInput(c.Id.ToString(), new AgentUserInput(c.AuthorId?.ToString() ?? string.Empty, c.AuthorName, string.Empty), c.CreatedAt, c.Body))],
            null,
            guidance,
            null,
            [.. ancestors.Select(a => Related(a) with { Type = a.Type })]);
    }

    private async Task<List<AgentAttachmentInput>> AttachmentsAsync(List<AgentAttachmentRow> rows, Func<AgentAttachmentRow, string> url, CancellationToken cancellationToken)
    {
        var result = new List<AgentAttachmentInput>(rows.Count);
        var remaining = InlineAttachmentBytes;
        var full = blobs is null;
        foreach (var row in rows)
        {
            string? text = null;
            if (!full && AttachmentKinds.Of(row.Name, row.ContentType) == AttachmentKind.Markdown)
            {
                var bytes = row.SizeBytes <= remaining ? await ReadAsync(row.StorageRef, remaining, cancellationToken) : null;
                if (bytes is null)
                {
                    full = true;
                }
                else
                {
                    remaining -= bytes.Length;
                    text = Encoding.UTF8.GetString(bytes);
                }
            }
            result.Add(new AgentAttachmentInput(row.Name, row.ContentType, row.SizeBytes, url(row), text));
        }
        return result;
    }

    // The blob's bytes, or null when it is missing or longer than limit.
    private async Task<byte[]?> ReadAsync(string storageRef, int limit, CancellationToken cancellationToken)
    {
        var blob = await blobs!.GetAsync(storageRef, cancellationToken);
        if (blob is null)
        {
            return null;
        }
        await using var stream = blob.Content;
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        return buffer.ToArray();
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
