using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.LinearApi;

// What an agent may read through /linear/graphql. Every method takes the calling connection's id and answers null (or an
// empty page) for anything that is not one of that connection's sessions or an item one of them is about, so a resolver can
// not leak another connection's data by forgetting a check. Reads only.
public sealed class LinearReads(NpgsqlDataSource dataSource)
{
    private const string Owned = "EXISTS (SELECT 1 FROM agent_session s WHERE s.work_item_id = w.id AND s.connection_id = @connectionId)";

    private const string IssueColumns =
        """
        w.id, w.key, w.number, w.title, w.description, w.acceptance_criteria::text AS criteria, to_jsonb(w.tags)::text AS tags,
        w.project_id AS projectId, p.key AS projectKey, w.state_id AS stateId, w.priority::int AS priority,
        w.created_at AS createdAt, w.changed_at AS updatedAt,
        w.created_by AS creatorId, cu.name AS creatorName, w.assigned_to AS assigneeId, au.name AS assigneeName,
        w.parent_id AS parentId,
        (WITH RECURSIVE up AS (
            SELECT a.id, a.type, a.parent_id FROM work_item a WHERE a.id = w.parent_id
            UNION ALL
            SELECT a.id, a.type, a.parent_id FROM work_item a JOIN up ON a.id = up.parent_id)
         SELECT id FROM up WHERE type = 'feature' LIMIT 1) AS featureId
        """;

    private const string IssueFrom =
        """
        FROM work_item w JOIN project p ON p.id = w.project_id
        LEFT JOIN app_user cu ON cu.id = w.created_by LEFT JOIN app_user au ON au.id = w.assigned_to
        """;

    // The instance's base URL, or the caller's origin when the instance has none.
    public async Task<string> BaseUrlAsync(string? fallbackBaseUrl, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var baseUrl = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT base_url FROM instance_settings", cancellationToken: cancellationToken));
        return string.IsNullOrWhiteSpace(baseUrl) ? fallbackBaseUrl ?? string.Empty : baseUrl;
    }

    // By item id or by key (SHIP-123), as Linear's issue(id:) takes either.
    public async Task<LinearIssue?> FindIssueAsync(Guid connectionId, string idOrKey, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<IssueRow>(new CommandDefinition(
            $"SELECT {IssueColumns} {IssueFrom} WHERE (w.id = @id OR w.key = @key) AND {Owned}",
            new { id = Guid.TryParse(idOrKey, out var parsed) ? parsed : Guid.Empty, key = idOrKey.Trim().ToUpperInvariant(), connectionId },
            cancellationToken: cancellationToken));
        return row?.ToIssue();
    }

    // Oldest first, `first` at a time, after the comment with id `after`. Empty when the item is not the connection's.
    public async Task<LinearCommentPage> CommentsAsync(Guid connectionId, Guid workItemId, int first, Guid? after, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<CommentRow>(new CommandDefinition(
            $"""
            SELECT c.id, c.work_item_id AS issueId, c.body, c.created_at AS createdAt, c.updated_at AS updatedAt, c.author_id AS authorId, u.name AS authorName
            FROM comment c JOIN work_item w ON w.id = c.work_item_id LEFT JOIN app_user u ON u.id = c.author_id
            WHERE c.work_item_id = @workItemId AND c.archived_at IS NULL AND {Owned}
              AND (@after::uuid IS NULL OR (c.created_at, c.id) > (SELECT a.created_at, a.id FROM comment a WHERE a.id = @after))
            ORDER BY c.created_at, c.id LIMIT @limit
            """,
            new { workItemId, connectionId, after, limit = first + 1 }, cancellationToken: cancellationToken))).ToList();
        return new LinearCommentPage(
            [.. rows.Take(first).Select(r => new LinearComment(r.Id, r.IssueId, r.Body, r.CreatedAt, r.UpdatedAt, Person(r.AuthorId, r.AuthorName)))],
            rows.Count > first);
    }

    public async Task<LinearSession?> FindSessionAsync(Guid connectionId, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return (await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(
            $"{SessionSelect} AND s.id = @sessionId", new { sessionId, connectionId }, cancellationToken: cancellationToken)))?.ToSession();
    }

    // The newest session of this connection on the item, for a query that names the item but no session.
    public async Task<LinearSession?> FindLatestSessionAsync(Guid connectionId, Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return (await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(
            $"{SessionSelect} AND s.work_item_id = @workItemId ORDER BY s.created_at DESC LIMIT 1", new { workItemId, connectionId }, cancellationToken: cancellationToken)))?.ToSession();
    }

    public async Task<LinearActivityPage> ActivitiesAsync(Guid connectionId, Guid sessionId, int first, Guid? after, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<LinearActivity>(new CommandDefinition(
            $"""
            SELECT {ActivityColumns} FROM agent_activity a JOIN agent_session s ON s.id = a.session_id
            WHERE a.session_id = @sessionId AND s.connection_id = @connectionId AND a.replaced_at IS NULL
              AND (@after::uuid IS NULL OR (a.created_at, a.id) > (SELECT x.created_at, x.id FROM agent_activity x WHERE x.id = @after))
            ORDER BY a.created_at, a.id LIMIT @limit
            """,
            new { sessionId, connectionId, after, limit = first + 1 }, cancellationToken: cancellationToken))).ToList();
        return new LinearActivityPage([.. rows.Take(first)], rows.Count > first);
    }

    public async Task<LinearActivity?> FindActivityAsync(Guid connectionId, Guid activityId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LinearActivity>(new CommandDefinition(
            $"SELECT {ActivityColumns} FROM agent_activity a JOIN agent_session s ON s.id = a.session_id WHERE a.id = @activityId AND s.connection_id = @connectionId AND a.replaced_at IS NULL",
            new { activityId, connectionId }, cancellationToken: cancellationToken));
    }

    // The repository the session was started with (issueRepositorySuggestions). A named session must be the connection's and be
    // about the item; with no session, the item's newest one. Null when neither is the connection's, or the URL is not a repository.
    public async Task<LinearRepositoryLookup> RepositoryAsync(Guid connectionId, string issueId, Guid? sessionId, CancellationToken cancellationToken)
    {
        var issue = await FindIssueAsync(connectionId, issueId, cancellationToken);
        if (issue is null)
        {
            return LinearRepositoryLookup.NotFound;
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var url = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            """
            SELECT repo_url FROM agent_session
            WHERE work_item_id = @workItemId AND connection_id = @connectionId AND (@sessionId::uuid IS NULL OR id = @sessionId)
            ORDER BY created_at DESC LIMIT 1
            """,
            new { workItemId = issue.Id, connectionId, sessionId }, cancellationToken: cancellationToken));
        return url is null ? LinearRepositoryLookup.NotFound : new LinearRepositoryLookup(true, LinearRepository.Parse(url));
    }

    private const string ActivityColumns =
        "a.id, a.session_id AS sessionId, a.type, a.body, a.action, a.parameter, a.result, a.ephemeral, a.signal, a.created_at AS createdAt";

    private const string SessionSelect =
        """
        SELECT s.id, s.work_item_id AS workItemId, w.key AS itemKey, p.key AS projectKey, s.state, s.external_urls::text AS externalUrls,
               s.plan::text AS plan, s.last_activity_at AS lastActivityAt, s.created_at AS createdAt, s.ended_at AS endedAt,
               s.created_by AS creatorId, coalesce(u.name, '') AS creatorName
        FROM agent_session s JOIN work_item w ON w.id = s.work_item_id JOIN project p ON p.id = w.project_id JOIN app_user u ON u.id = s.created_by
        WHERE s.connection_id = @connectionId
        """;

    private static LinearPerson? Person(Guid? id, string? name) => id is { } value ? new LinearPerson(value, name ?? string.Empty) : null;

    private sealed record IssueRow(
        Guid Id, string Key, int Number, string Title, string Description, string Criteria, string Tags, Guid ProjectId, string ProjectKey, Guid StateId,
        int Priority, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid? CreatorId, string? CreatorName, Guid? AssigneeId, string? AssigneeName,
        Guid? ParentId, Guid? FeatureId)
    {
        public LinearIssue ToIssue()
        {
            var criteria = JsonSerializer.Deserialize<List<ChecklistItem>>(Criteria, PersistedJson.Options) ?? [];
            return new LinearIssue(
                Id, Key, Number, Title,
                LinearDescription.WithCriteria(Description, criteria),
                JsonSerializer.Deserialize<string[]>(Tags, PersistedJson.Options) ?? [],
                ProjectId, ProjectKey, StateId, Priority, CreatedAt, UpdatedAt,
                Person(CreatorId, CreatorName), Person(AssigneeId, AssigneeName), ParentId, FeatureId);
        }
    }

    private sealed record CommentRow(Guid Id, Guid IssueId, string Body, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid? AuthorId, string? AuthorName);

    private sealed record SessionRow(
        Guid Id, Guid WorkItemId, string ItemKey, string ProjectKey, string State, string ExternalUrls, string Plan, DateTimeOffset LastActivityAt,
        DateTimeOffset CreatedAt, DateTimeOffset? EndedAt, Guid CreatorId, string CreatorName)
    {
        public LinearSession ToSession() => new(
            Id, WorkItemId, ItemKey, ProjectKey, State, ExternalUrls, Plan, LastActivityAt, CreatedAt, EndedAt, new LinearPerson(CreatorId, CreatorName));
    }
}
