using Dapper;
using Npgsql;
using ShipLean.Application.Criteria;
using ShipLean.Contracts;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Core;

namespace ShipLean.Application.Features.Mcp.Orient;

// The SQL behind the Orient tools. Access is the caller's job: every project or item id passed in was resolved through
// the MCP resolvers first. Rolled-up and change reads are not repeated here (IWorkTreeReads, IWorkItemChanges).
public sealed class OrientReads(NpgsqlDataSource dataSource, ICurrentUser currentUser, ICriteriaEvaluator criteria, TimeProvider clock)
{
    public const int SearchCap = 25;

    // Candidates read before ranking; more than the cap so the ranking has something to choose from.
    private const int CandidateLimit = 500;

    // The name of one live Markdown attachment whose text holds a searched word; NULL when none does.
    private const string AttachmentMatch =
        "(SELECT a.name FROM attachment a WHERE a.work_item_id = w.id AND a.archived_at IS NULL AND a.search_text ILIKE ANY(@patterns) ORDER BY a.name LIMIT 1)";

    // projectId: only that project; null = every project the user is in.
    public async Task<IReadOnlyList<ProjectSummary>> ProjectsAsync(Guid? projectId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProjectRow>(new CommandDefinition(
            $"""
            SELECT p.id, p.key, p.name, m.role AS roleText, sp.id AS sprintId, sp.name AS sprintName,
                   to_char(sp.start_date, 'YYYY-MM-DD') AS sprintStart, to_char(sp.end_date, 'YYYY-MM-DD') AS sprintEnd
            FROM project p
            JOIN project_member m ON m.project_id = p.id AND m.user_id = @me AND m.archived_at IS NULL
            LEFT JOIN LATERAL (
                SELECT s.id, s.name, s.start_date, s.end_date FROM sprint s
                WHERE s.project_id = p.id AND {CurrentSprintSql.Where}
                ORDER BY s.start_date LIMIT 1) sp ON true
            WHERE @projectId::uuid IS NULL OR p.id = @projectId
            ORDER BY p.key
            """,
            new { me = currentUser.Id, today, projectId }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToSummary())];
    }

    public async Task<ItemNames> NamesAsync(Guid itemId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var item = await connection.QuerySingleOrDefaultAsync<NameRow>(new CommandDefinition(
            """
            SELECT p.key AS projectKey, s.name AS stateName, u.name AS assignee, sp.name AS sprint
            FROM work_item w
            JOIN project p ON p.id = w.project_id
            JOIN state s ON s.id = w.state_id
            LEFT JOIN app_user u ON u.id = w.assigned_to
            LEFT JOIN sprint sp ON sp.id = w.sprint_id
            WHERE w.id = @itemId
            """,
            new { itemId }, cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("No work item with that key was found.");

        // Nearest parent first. The depth bound ends a walk through bad data; the schema allows no cycle in normal use.
        var parents = await connection.QueryAsync<ParentName>(new CommandDefinition(
            """
            WITH RECURSIVE chain (id, parent_id, key, title, type, depth) AS (
                SELECT p.id, p.parent_id, p.key, p.title, p.type, 1 FROM work_item w JOIN work_item p ON p.id = w.parent_id WHERE w.id = @itemId
                UNION ALL
                SELECT p.id, p.parent_id, p.key, p.title, p.type, c.depth + 1 FROM chain c JOIN work_item p ON p.id = c.parent_id WHERE c.depth < 10
            )
            SELECT key, title, type FROM chain ORDER BY depth
            """,
            new { itemId }, cancellationToken: cancellationToken));

        var users = userIds.Count == 0
            ? []
            : (await connection.QueryAsync<UserRow>(new CommandDefinition(
                "SELECT id, name FROM app_user WHERE id = ANY(@ids)", new { ids = userIds.ToArray() }, cancellationToken: cancellationToken))).ToDictionary(u => u.Id, u => u.Name);

        return new ItemNames(item.ProjectKey, item.StateName, item.Assignee, item.Sprint, [.. parents], users);
    }

    // The ranked page: SQL ILIKE finds candidates (any word in title or description), WordOverlap ranks them, title hits first, then description, then Markdown attachment text.
    public async Task<SearchPage> SearchAsync(SearchFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var parameters = new DynamicParameters(new { projectId = filter.ProjectId });
        var conditions = new List<string> { "w.project_id = @projectId", "s.category <> 'removed'" };

        var clauses = new List<CriteriaClause>();
        if (filter.Type is { } type)
        {
            clauses.Add(CriteriaClauses.Of(FieldIds.Type, CriteriaOp.Equal, type.ToString().ToLowerInvariant()));
        }
        if (filter.StateName is { } stateName)
        {
            clauses.Add(CriteriaClauses.Of(FieldIds.State, CriteriaOp.In, await StateIdsByNameAsync(connection, filter.ProjectId, stateName, cancellationToken)));
        }
        if (filter.State is { } stateId)
        {
            clauses.Add(CriteriaClauses.Of(FieldIds.State, CriteriaOp.In, [stateId.ToString()]));
        }
        if (filter.Assignee is { } assignee)
        {
            clauses.Add(CriteriaClauses.Of(FieldIds.Assignee, CriteriaOp.Equal, assignee.ToString()));
        }
        if (filter.Sprint is { } sprint)
        {
            clauses.Add(CriteriaClauses.Of(FieldIds.Sprint, CriteriaOp.Equal, sprint.ToString()));
        }
        if (filter.Tag is { } tag)
        {
            clauses.Add(CriteriaClauses.Of(FieldIds.Tags, CriteriaOp.Contains, tag));
        }
        if (filter.Parent is { } parent)
        {
            clauses.Add(CriteriaClauses.Of(FieldIds.Parent, CriteriaOp.Equal, parent.ToString()));
        }
        if (clauses.Count > 0)
        {
            var fragment = criteria.ToSql(new CriteriaJson(CriteriaMatch.All, clauses), "w");
            foreach (var (name, value) in fragment.Parameters)
            {
                parameters.Add(name, value);
            }
            conditions.Add(fragment.Where);
        }

        var patterns = filter.Words.Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => $"%{Escape(w.Trim())}%").ToArray();
        if (patterns.Length > 0)
        {
            parameters.Add("patterns", patterns);
            conditions.Add($"(w.title ILIKE ANY(@patterns) OR w.description ILIKE ANY(@patterns) OR {AttachmentMatch} IS NOT NULL)");
        }

        var matchColumns = patterns.Length > 0
            ? $"{AttachmentMatch} AS attachmentFile, NOT (w.title ILIKE ANY(@patterns) OR w.description ILIKE ANY(@patterns)) AS attachmentOnly"
            : "NULL::text AS attachmentFile, false AS attachmentOnly";
        var sql =
            $"""
            SELECT w.id, w.key, w.title, w.type, s.name AS state, u.name AS assignee, sp.name AS sprint, w.story_points AS points,
                   p.key AS parent, w.tags AS tags, w.changed_at AS changedAt, {matchColumns}
            FROM work_item w
            JOIN state s ON s.id = w.state_id
            LEFT JOIN app_user u ON u.id = w.assigned_to
            LEFT JOIN sprint sp ON sp.id = w.sprint_id
            LEFT JOIN work_item p ON p.id = w.parent_id
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY w.changed_at DESC, w.id
            LIMIT {CandidateLimit}
            """;
        var rows = await HitsAsync(connection, sql, parameters, cancellationToken);

        var ranked = rows
            .Select(r => (Hit: r.ToHit(WordOverlap.Score(r.Title, filter.Words)), r.AttachmentOnly))
            .OrderByDescending(r => r.Hit.Score)
            .ThenBy(r => r.AttachmentOnly)
            .Select(r => r.Hit)
            .ToList();
        return new SearchPage([.. ranked.Take(SearchCap)], ranked.Count > SearchCap);
    }

    private static async Task<List<HitRow>> HitsAsync(NpgsqlConnection connection, string sql, DynamicParameters parameters, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<HitRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))];

    // A state name matches the states of every type of the project, ignoring case, or by its start when no name is equal.
    // The caller checks project access.
    public async Task<string[]> StateIdsByNameAsync(Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await StateIdsByNameAsync(connection, projectId, name, cancellationToken);
    }

    private static async Task<string[]> StateIdsByNameAsync(NpgsqlConnection connection, Guid projectId, string name, CancellationToken cancellationToken)
    {
        var wanted = name.Trim();
        var states = (await connection.QueryAsync<StateRow>(new CommandDefinition(
            "SELECT id, name FROM state WHERE project_id = @projectId AND archived_at IS NULL", new { projectId }, cancellationToken: cancellationToken))).ToList();
        var matches = states.Where(s => string.Equals(s.Name, wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
        {
            matches = [.. states.Where(s => s.Name.StartsWith(wanted, StringComparison.OrdinalIgnoreCase))];
        }
        return matches.Count == 0
            ? throw ApiException.NotFound($"No state matches \"{wanted}\".")
            : [.. matches.Select(s => s.Id.ToString())];
    }

    // ILIKE treats backslash, percent and underscore as pattern characters.
    private static string Escape(string word) => word.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private sealed record ProjectRow(Guid Id, string Key, string Name, string RoleText, Guid? SprintId, string? SprintName, string? SprintStart, string? SprintEnd)
    {
        public ProjectSummary ToSummary()
            => new(Id, Key, Name, Enum.Parse<ProjectRole>(RoleText, true), SprintId is { } id ? new SprintSummary(id, SprintName!, SprintStart!, SprintEnd!) : null);
    }

    private sealed record NameRow(string ProjectKey, string StateName, string? Assignee, string? Sprint);

    private sealed record UserRow(Guid Id, string Name);

    private sealed record StateRow(Guid Id, string Name);

    private sealed record HitRow(Guid Id, string Key, string Title, string Type, string State, string? Assignee, string? Sprint, decimal? Points, string? Parent, Array Tags, DateTimeOffset ChangedAt, string? AttachmentFile, bool AttachmentOnly)
    {
        public SearchHit ToHit(int score) => new(Id, Key, Title, Type, State, Assignee, Sprint, Points, Parent, [.. (string[])Tags], score, AttachmentFile);
    }
}
