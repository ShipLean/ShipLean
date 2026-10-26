using Dapper;
using Npgsql;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItems.Reads.Tree;

// The tree reads over Postgres. Definitions of open, closed and stack order live in TreeSql.
public sealed class WorkTreeReads(NpgsqlDataSource dataSource, ICriteriaEvaluator criteria, TimeProvider time) : IWorkTreeReads
{
    private const string FilterAlias = "w";

    public async Task<IReadOnlyList<WorkTreeItemDto>> DescendantsAsync(Guid projectId, Guid itemId, WorkItemTypeId? type, CancellationToken cancellationToken = default)
    {
        var sql =
            $"""
            {TreeSql.DescendantsCte}
            SELECT {TreeSql.ItemColumns}, t.depth AS depth
            FROM tree t JOIN work_item w ON w.id = t.id
            {TreeSql.ItemJoins}
            WHERE {TreeSql.Live} AND (@type::text IS NULL OR w.type = @type)
            ORDER BY ({TreeSql.Closed}), {TreeSql.StackOrder}
            """;
        var parameters = new DynamicParameters(new { projectId, roots = new[] { itemId }, type = type is { } t ? WorkItemTypes.Db(t) : null });
        return await ItemsAsync(sql, parameters, row => row.ToItem(SprintLabels.Today(time)), cancellationToken);
    }

    public async Task<IReadOnlyList<WorkTreeRollupDto>> RollupsAsync(Guid projectId, IReadOnlyCollection<Guid> parentIds, CriteriaJson? filter, CancellationToken cancellationToken = default)
    {
        if (parentIds.Count == 0)
        {
            return [];
        }

        var parameters = new DynamicParameters(new { projectId, roots = parentIds.Distinct().ToArray() });
        var sql =
            $"""
            {TreeSql.DescendantsCte}
            SELECT t.root_id AS parentId, w.type,
                   count(*) FILTER (WHERE {TreeSql.Open})::int AS open,
                   count(*) FILTER (WHERE {TreeSql.Closed})::int AS closed,
                   sum(w.story_points) FILTER (WHERE {TreeSql.Open}) AS openPoints,
                   sum(w.story_points) AS totalPoints,
                   count(*) FILTER (WHERE w.story_points IS NULL)::int AS unestimated
            FROM tree t JOIN work_item w ON w.id = t.id
            JOIN state s ON s.id = w.state_id
            WHERE {TreeSql.Live}{FilterSql(filter, parameters)}
            GROUP BY t.root_id, w.type
            ORDER BY t.root_id, w.type
            """;
        return await RollupRowsAsync(sql, parameters, cancellationToken);
    }

    public async Task<IReadOnlyList<StackRowDto>> StackAsync(Guid projectId, StackLevel level, CriteriaJson? filter, bool includeCompleted, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters(new { projectId, types = Types(level) });
        var sql =
            $"""
            SELECT {TreeSql.ItemColumns}, 0 AS depth
            FROM work_item w
            {TreeSql.ItemJoins}
            WHERE w.project_id = @projectId AND w.type = ANY(@types) AND {TreeSql.Live}
              {(includeCompleted ? string.Empty : $"AND NOT ({TreeSql.Closed})")}{FilterSql(filter, parameters)}
            ORDER BY {TreeSql.StackOrder}
            """;
        return await ItemsAsync(sql, parameters, row => row.ToStackRow(SprintLabels.Today(time)), cancellationToken);
    }

    public async Task<IReadOnlyList<WorkTreeItemDto>> ReadyAsync(Guid projectId, Guid sprintId, WorkItemTypeId? type, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var sql =
            $"""
            {TreeSql.DescendantsCte}
            SELECT {TreeSql.ItemColumns}, coalesce(t.depth, 0) AS depth
            FROM work_item w
            {TreeSql.ItemJoins}
            LEFT JOIN tree t ON t.id = w.id
            WHERE w.project_id = @projectId AND w.sprint_id = @sprintId AND w.assigned_to IS NULL AND s.category = 'proposed'
              AND (@type::text IS NULL OR w.type = @type) AND (@parentId::uuid IS NULL OR t.id IS NOT NULL)
            ORDER BY {TreeSql.StackOrder}
            """;
        var parameters = new DynamicParameters(new { projectId, sprintId, type = type is { } t ? WorkItemTypes.Db(t) : null, parentId, roots = parentId is { } p ? new[] { p } : Array.Empty<Guid>() });
        return await ItemsAsync(sql, parameters, row => row.ToItem(SprintLabels.Today(time)), cancellationToken);
    }

    // The levels' types: Requirement is Stories and Bugs.
    private static string[] Types(StackLevel level) => level switch
    {
        StackLevel.Requirement => [WorkItemTypes.Db(WorkItemTypeId.Story), WorkItemTypes.Db(WorkItemTypeId.Bug)],
        StackLevel.Feature => [WorkItemTypes.Db(WorkItemTypeId.Feature)],
        _ => [WorkItemTypes.Db(WorkItemTypeId.Epic)],
    };

    // " AND (<fragment>)" with the fragment's parameters added, or nothing without a filter. Values are never in the text.
    private string FilterSql(CriteriaJson? filter, DynamicParameters parameters)
    {
        if (filter is null)
        {
            return string.Empty;
        }

        var sql = criteria.ToSql(filter, FilterAlias);
        foreach (var (name, value) in sql.Parameters)
        {
            parameters.Add(name, value);
        }

        return $" AND ({sql.Where})";
    }

    private async Task<IReadOnlyList<T>> ItemsAsync<T>(string sql, DynamicParameters parameters, Func<ItemRow, T> map, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ItemRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return [.. rows.Select(map)];
    }

    private async Task<IReadOnlyList<WorkTreeRollupDto>> RollupRowsAsync(string sql, DynamicParameters parameters, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<RollupRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return [.. rows.Select(r => new WorkTreeRollupDto(r.ParentId, WorkItemTypes.Parse(r.Type), r.Open, r.Closed, r.OpenPoints, r.TotalPoints, r.Unestimated))];
    }

    private sealed record RollupRow(Guid ParentId, string Type, int Open, int Closed, decimal? OpenPoints, decimal? TotalPoints, int Unestimated);
}
