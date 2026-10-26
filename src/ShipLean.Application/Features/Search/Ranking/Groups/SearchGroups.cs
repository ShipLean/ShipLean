using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Ranking.Groups;

// The project and sprint matches beside the items. Projects match lexically on their `project` row ("KEY Name\ndescription"). Sprints
// match lexically on `sprint_name` and `sprint_goal`, and semantically on `sprint_goal` when the query has a vector and the row was
// embedded by the loaded model; the two lists are fused by reciprocal rank. Access (project_member) and archived sprints are in the SQL.
public sealed class SearchGroups(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    IEmbedder embedder,
    SearchOptions options) : ISearchGroups
{
    // ts_rank_cd takes the weights as {D, C, B, A}: the CB1 weights, as in the lexical item leg.
    private const string Weights = "'{0.1, 0.2, 0.4, 1.0}'";

    public async Task<IReadOnlyList<SearchResultProject>> ProjectsAsync(CandidateQuery query, CancellationToken cancellationToken = default)
    {
        var (head, last) = SearchTsQuery.Split(query.Text, query.Prefix);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProjectRow>(new CommandDefinition(
            ProjectsSql(SearchTsQuery.Expression(last is not null)),
            new { me = currentUser.Id, projectIds = query.ProjectIds.ToArray(), head, last, top = options.TopPerList },
            cancellationToken: cancellationToken));
        return [.. rows.Select(r => new SearchResultProject(r.Key, r.Name))];
    }

    // The SQL text holds only fixed fragments; every value is a parameter.
    private static string ProjectsSql(string tsquery)
        => $"""
            WITH q AS (SELECT {tsquery} AS query)
            SELECT p.key AS Key, p.name AS Name
            FROM search_doc d
            CROSS JOIN q
            JOIN project_member m ON m.project_id = d.project_id AND m.user_id = @me AND m.archived_at IS NULL
            JOIN project p ON p.id = d.source_id AND p.id = d.project_id
            WHERE d.source_kind = 'project' AND d.field = 'project' AND d.project_id = ANY(@projectIds) AND d.tsv @@ q.query
            ORDER BY ts_rank_cd({Weights}, d.tsv, q.query) DESC, p.key
            LIMIT @top
            """;

    public async Task<IReadOnlyList<SearchResultSprint>> SprintsAsync(CandidateQuery query, CancellationToken cancellationToken = default)
    {
        var (head, last) = SearchTsQuery.Split(query.Text, query.Prefix);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var parameters = new { me = currentUser.Id, projectIds = query.ProjectIds.ToArray(), head, last, top = options.TopPerList };
        var lexical = await connection.QueryAsync<Guid>(new CommandDefinition(
            SprintsSql(SearchTsQuery.Expression(last is not null)),
            parameters,
            cancellationToken: cancellationToken));

        var semantic = await SemanticAsync(connection, query, cancellationToken);
        var scores = new Dictionary<Guid, double>();
        foreach (var list in new[] { lexical.ToList(), semantic })
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                scores[list[rank]] = scores.GetValueOrDefault(list[rank]) + 1.0 / (options.RrfK + rank + 1);
            }
        }

        if (scores.Count == 0)
        {
            return [];
        }

        var ids = scores.OrderByDescending(s => s.Value).ThenBy(s => s.Key).Take(options.TopPerList).Select(s => s.Key).ToArray();
        var rows = (await connection.QueryAsync<SprintRow>(new CommandDefinition(
            """
            SELECT s.id AS Id, s.name AS Name, s.goal AS Goal, s.start_date AS StartDate, s.end_date AS EndDate, p.key AS ProjectKey
            FROM sprint s
            JOIN project p ON p.id = s.project_id
            WHERE s.id = ANY(@ids)
            """,
            new { ids },
            cancellationToken: cancellationToken))).ToDictionary(r => r.Id);
        return [.. ids.Select(id => rows[id]).Select(r => new SearchResultSprint(r.Id, r.Name, r.Goal, r.StartDate, r.EndDate, r.ProjectKey))];
    }

    private static string SprintsSql(string tsquery)
        => $"""
            WITH q AS (SELECT {tsquery} AS query)
            SELECT s.id
            FROM search_doc d
            CROSS JOIN q
            JOIN project_member m ON m.project_id = d.project_id AND m.user_id = @me AND m.archived_at IS NULL
            JOIN sprint s ON s.id = d.source_id AND s.project_id = d.project_id AND s.archived_at IS NULL
            WHERE d.source_kind = 'sprint' AND d.field IN ('sprint_name', 'sprint_goal') AND d.project_id = ANY(@projectIds) AND d.tsv @@ q.query
            GROUP BY s.id
            ORDER BY max(ts_rank_cd({Weights}, d.tsv, q.query)) DESC, s.id
            LIMIT @top
            """;

    // Sprint ids whose goal embedding is at least Cutoff close to the query, nearest first. Both vectors are L2-normalised, so cosine is the dot product.
    private async Task<List<Guid>> SemanticAsync(NpgsqlConnection connection, CandidateQuery query, CancellationToken cancellationToken)
    {
        if (query.Vector is null || embedder.Status.Mode != EmbedderMode.Semantic || embedder.ModelId is not { } model)
        {
            return [];
        }

        var rows = await connection.QueryAsync<GoalRow>(new CommandDefinition(
            """
            SELECT s.id AS Id, d.embedding AS Embedding
            FROM search_doc d
            JOIN project_member m ON m.project_id = d.project_id AND m.user_id = @me AND m.archived_at IS NULL
            JOIN sprint s ON s.id = d.source_id AND s.project_id = d.project_id AND s.archived_at IS NULL
            WHERE d.source_kind = 'sprint' AND d.field = 'sprint_goal' AND d.project_id = ANY(@projectIds)
              AND d.embedding IS NOT NULL AND d.embedding_model = @model
            """,
            new { me = currentUser.Id, projectIds = query.ProjectIds.ToArray(), model = model.ToString() },
            cancellationToken: cancellationToken));
        return
        [
            .. rows
                .Where(r => r.Embedding is float[] e && e.Length == query.Vector.Length)
                .Select(r => (r.Id, Cosine: Dot(query.Vector, (float[])r.Embedding)))
                .Where(r => r.Cosine >= options.Cutoff)
                .OrderByDescending(r => r.Cosine)
                .ThenBy(r => r.Id)
                .Take(options.TopPerList)
                .Select(r => r.Id),
        ];
    }

    private static double Dot(float[] a, float[] b)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    private sealed record ProjectRow(string Key, string Name);

    private sealed record SprintRow(Guid Id, string Name, string Goal, DateOnly StartDate, DateOnly EndDate, string ProjectKey);

    // Dapper hands a real[] to a record constructor as System.Array.
    private sealed record GoalRow(Guid Id, Array Embedding);
}
