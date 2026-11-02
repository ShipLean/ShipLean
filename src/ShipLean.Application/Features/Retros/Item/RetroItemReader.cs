using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Reads;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Item;

// The item itself is read for real (so an unknown key is not_found); everything about its retros is empty.
// TODO: the real item rollup replaces the empty totals, board, actions and children.
public sealed class RetroItemReader(NpgsqlDataSource dataSource) : IRetroItemReader
{
    public async Task<RetroItemDto> GetAsync(Guid projectId, string itemKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ItemRow>(new CommandDefinition(
            """
            SELECT w.id, w.key, w.type, w.title, st.id AS stateId, st.name AS stateName, st.category AS stateCategory
            FROM work_item w JOIN state st ON st.id = w.state_id
            WHERE w.project_id = @projectId AND w.key = @key
            """,
            new { projectId, key = itemKey.Trim().ToUpperInvariant() },
            cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("No work item with that key was found.");
        var type = Enum.Parse<WorkItemTypeId>(row.Type, ignoreCase: true);
        return new RetroItemDto(
            new RetroItemRefDto(row.Id, row.Key, new TypeRef(type, WorkItemTypeRules.Name(type)), row.Title),
            new StateRef(row.StateId, row.StateName, Enum.Parse<StateCategory>(row.StateCategory, ignoreCase: true)),
            null,
            RetroAggregates.Of([]),
            [.. RetroCategories.Display.Select(category => new RetroCategoryColumnDto(category, [], 0))],
            [],
            [],
            []);
    }

    private sealed record ItemRow(Guid Id, string Key, string Type, string Title, Guid StateId, string StateName, string StateCategory);
}
