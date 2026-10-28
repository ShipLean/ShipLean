using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch.Rendering;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.AgentDispatch;

// Maps an item's rows onto the renderer's plain input. Reads only; the session id and creation time are the caller's.
internal sealed class AgentContextLoader(NpgsqlDataSource dataSource)
{
    public async Task<AgentContextInput> LoadAsync(
        Guid workItemId, Guid sessionId, DateTimeOffset createdAt, Guid connectionId, Guid creatorId, AgentRepositoryInput repository, string? fallbackBaseUrl, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var item = await connection.QuerySingleOrDefaultAsync<AgentItemRow>(new CommandDefinition(
            """
            SELECT w.id, w.key, w.title, w.description, w.acceptance_criteria::text AS criteria, to_jsonb(w.tags)::text AS tags, p.id AS projectId, p.key AS projectKey, p.name AS projectName
            FROM work_item w JOIN project p ON p.id = w.project_id
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
            SELECT c.id, c.author_id AS authorId, coalesce(u.name, '') AS authorName, coalesce(u.email, '') AS authorEmail, c.created_at AS createdAt, c.body
            FROM comment c LEFT JOIN app_user u ON u.id = c.author_id
            WHERE c.work_item_id = @workItemId AND c.archived_at IS NULL
            ORDER BY c.created_at, c.id
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var baseUrl = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT base_url FROM instance_settings", cancellationToken: cancellationToken));
        baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? fallbackBaseUrl ?? string.Empty : baseUrl;

        string Url(string key) => McpOpenUrls.WorkItem(baseUrl, item.ProjectKey, key);
        AgentRelatedItemInput Related(AgentRelatedRow row) => new(row.Id.ToString(), row.Key, row.Title, Blank(row.Description), Url(row.Key));
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
            new AgentItemInput(item.Id.ToString(), item.Key, item.Title, Blank(item.Description), Url(item.Key), JsonSerializer.Deserialize<string[]>(item.Tags, PersistedJson.Options) ?? [], [.. criteria.Select(c => new AgentCriterionInput(c.Text, c.Done))]),
            parent is null ? null : Related(parent),
            feature is null ? null : Related(feature),
            [.. comments.Select(c => new AgentCommentInput(c.Id.ToString(), new AgentUserInput(c.AuthorId?.ToString() ?? string.Empty, c.AuthorName, c.AuthorEmail), c.CreatedAt, c.Body))],
            null);
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
