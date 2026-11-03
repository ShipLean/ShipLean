using Dapper;
using Npgsql;
using ShipLean.Application.Features.Search.Ranking.Fusion;
using ShipLean.Contracts;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Ranking.Keys;

// The key leg: while the caller is typing (Prefix), a query shaped like part of a key (KeyPart) matches the keys that contain it.
// Keys are not in search_doc, so this reads work_item. A bare number is compared with the item number, not the key, so "43" can rank
// 43, then 430, then 143. Best first: the whole key or number, then starting with the part, then containing it, each by item number.
// Access, removed items and the item filters are in the SQL, as in the other legs.
public sealed class KeyItemCandidates(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    ICriteriaEvaluator criteria,
    SearchOptions options) : IItemCandidates
{
    private const string WholeKey = "w.key";
    private const string Number = "w.number::text";

    public CandidateLeg Leg => CandidateLeg.Key;

    public async Task<CandidateLists> ListsAsync(CandidateQuery query, CancellationToken cancellationToken = default)
    {
        if (!query.Prefix || !KeyPart.TryParse(query.Text, out var part))
        {
            return new CandidateLists(true, 0, []);
        }

        var parameters = new DynamicParameters(new
        {
            me = currentUser.Id,
            projectIds = query.ProjectIds.ToArray(),
            part,
            top = options.TopPerList,
        });
        var filters = ItemFilterSql.Build(query, criteria, parameters);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<HitRow>(new CommandDefinition(
            Sql(char.IsAsciiDigit(part[0]) ? Number : WholeKey, filters),
            parameters,
            cancellationToken: cancellationToken));
        return new CandidateLists(true, 0, [new RankedList(RankedListName.KeyPart, [.. rows.Select(r => new CandidateHit(r.WorkItemId, SearchField.Title, r.Score, ""))])]);
    }

    // The SQL text holds only fixed fragments; every value is a parameter.
    private static string Sql(string target, string filters)
        => $"""
            SELECT w.id AS WorkItemId,
                   (CASE WHEN {target} = @part THEN 3 WHEN starts_with({target}, @part) THEN 2 ELSE 1 END)::float8 AS Score
            FROM work_item w
            JOIN state s ON s.id = w.state_id
            JOIN project_member m ON m.project_id = w.project_id AND m.user_id = @me AND m.archived_at IS NULL
            WHERE w.project_id = ANY(@projectIds) AND strpos({target}, @part) > 0
              AND s.category <> 'removed'{filters}
            ORDER BY Score DESC, w.number, w.key
            LIMIT @top
            """;

    private sealed record HitRow(Guid WorkItemId, double Score);
}
