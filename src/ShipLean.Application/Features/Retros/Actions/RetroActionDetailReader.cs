using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Actions;

// The action item's own ref is read for real (so a work item that is not an action is not_found); its points, theme and raised sprint are empty.
// TODO: the real detail (live points, theme, sprint it was raised in, fix type) replaces this.
public sealed class RetroActionDetailReader(NpgsqlDataSource dataSource) : IRetroActionDetailReader
{
    public async Task<RetroActionDetailDto> GetAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ActionRow>(new CommandDefinition(
            """
            SELECT w.id AS workItemId, w.key, w.title, st.id AS stateId, st.name AS stateName, st.category AS stateCategory
            FROM retro_action ra JOIN work_item w ON w.id = ra.work_item_id JOIN state st ON st.id = w.state_id
            WHERE ra.work_item_id = @workItemId
            """,
            new { workItemId },
            cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("No action item with that id was found.");
        var state = new StateRef(row.StateId, row.StateName, Enum.Parse<StateCategory>(row.StateCategory, ignoreCase: true));
        return new RetroActionDetailDto(new RetroActionRefDto(row.WorkItemId, row.Key, row.Title, state, null, null, null, 0), null, [], null);
    }

    private sealed record ActionRow(Guid WorkItemId, string Key, string Title, Guid StateId, string StateName, string StateCategory);
}
