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
        return new TriageRead(ids, rows.Sum(r => r.Count), [.. ids.Except(Named(criteria, ids))]);
    }

    // The states a state clause picks by id (= or in). Every other intake state stays hidden, whatever the match mode.
    private static HashSet<Guid> Named(CriteriaJson? criteria, IReadOnlyCollection<Guid> ids) =>
        criteria is null
            ? []
            : [.. criteria.Clauses.Where(c => c.Field == "state" && c.Op is CriteriaOp.Equal or CriteriaOp.In)
                .SelectMany(c => Picked(c.Value)).Where(ids.Contains)];

    private static IEnumerable<Guid> Picked(System.Text.Json.JsonElement value) =>
        value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => Guid.TryParse(value.GetString(), out var id) ? [id] : [],
            System.Text.Json.JsonValueKind.Array => value.EnumerateArray().SelectMany(Picked),
            _ => [],
        };
}

// StateIds: every intake state of the types. Count: the items in them. Hidden: the intake states the read leaves out (those the filter doesn't name).
public sealed record TriageRead(IReadOnlyList<Guid> StateIds, int Count, IReadOnlyList<Guid> Hidden)
{
    // The rows outside the hidden states.
    public IReadOnlyList<StackRowDto> Visible(IReadOnlyList<StackRowDto> rows) =>
        Hidden.Count == 0 ? rows : [.. rows.Where(r => !Hidden.Contains(r.State.Id))];
}
