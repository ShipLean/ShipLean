using Dapper;
using Npgsql;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Backlog.Shared;

// Items in an intake (Triage) state are kept out of the backlogs: a stranger's GitHub issue waits there for a person. The backlog
// reads leave them out, count them for the toolbar chip, and show them only when the filter names an intake state.
public sealed class BacklogTriage(NpgsqlDataSource dataSource)
{
    private const string Sql =
        """
        SELECT s.id, count(w.id)::int AS count
        FROM state s
        LEFT JOIN work_item w ON w.state_id = s.id AND w.project_id = s.project_id
        WHERE s.project_id = @projectId AND s.is_intake AND s.type = ANY(@types)
        GROUP BY s.id
        ORDER BY s.sort_order
        """;

    // The intake states of the types (as the database stores them), each with the items waiting in it.
    public async Task<TriageRead> ReadAsync(Guid projectId, IReadOnlyList<string> dbTypes, CriteriaJson? criteria, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<(Guid Id, int Count)>(new CommandDefinition(
            Sql, new { projectId, types = dbTypes }, cancellationToken: cancellationToken))).ToList();
        var ids = rows.Select(r => r.Id).ToList();
        return new TriageRead(ids, rows.Sum(r => r.Count), NamesAny(criteria, ids) ? [] : [.. ids]);
    }

    // Whether a state clause picks one of the states by id (= or in).
    private static bool NamesAny(CriteriaJson? criteria, IReadOnlyCollection<Guid> ids) =>
        criteria is not null && criteria.Clauses.Any(c =>
            c.Field == "state" && c.Op is CriteriaOp.Equal or CriteriaOp.In && Picks(c.Value, ids));

    private static bool Picks(System.Text.Json.JsonElement value, IReadOnlyCollection<Guid> ids) =>
        value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => Guid.TryParse(value.GetString(), out var id) && ids.Contains(id),
            System.Text.Json.JsonValueKind.Array => value.EnumerateArray().Any(v => Picks(v, ids)),
            _ => false,
        };
}

// StateIds: every intake state of the types. Count: the items in them. Hidden: the states the read leaves out (none when the filter names one).
public sealed record TriageRead(IReadOnlyList<Guid> StateIds, int Count, IReadOnlyList<Guid> Hidden)
{
    // The rows outside the hidden states.
    public IReadOnlyList<StackRowDto> Visible(IReadOnlyList<StackRowDto> rows) =>
        Hidden.Count == 0 ? rows : [.. rows.Where(r => !Hidden.Contains(r.State.Id))];
}
