using Dapper;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using System.Text.Json;

namespace ShipLean.Application.Features.WorkItems;

// Where a new item lands: the first column of its level's board that maps its state (the board is read FOR SHARE, as every card placement does).
internal static class WorkItemPlacement
{
    // Column, lane and column order for a new item in the given state; all null when the type has no board column for it
    // (tasks live on the taskboard, whose columns are states) or no column maps the state.
    public static async Task<(string? ColumnId, string? LaneId, string? ColumnOrder)> InitialAsync(
        WriteScope scope, Guid projectId, Guid itemId, WorkItemTypeId type, Guid stateId, CancellationToken cancellationToken)
    {
        if (WorkItemTypes.BoardLevel(type) is not { } level)
        {
            return (null, null, null);
        }

        var json = await scope.Connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT settings::text FROM board WHERE project_id = @projectId AND level = @level FOR SHARE",
            new { projectId, level }, scope.Transaction, cancellationToken: cancellationToken));
        if (json is null)
        {
            return (null, null, null);
        }

        var settings = JsonSerializer.Deserialize<BoardSettingsJson>(json, PersistedJson.Options)!;
        var column = settings.Columns.FirstOrDefault(c => c.StateMappings.Any(m => m.Type == type && m.StateId == stateId));
        if (column is null)
        {
            return (null, null, null);
        }

        var lane = settings.Swimlanes.FirstOrDefault(l => l.Kind == SwimlaneKind.Default)?.Id;
        return (column.Id, lane, await WorkItemOrdering.KeyAsync(scope, OrderScope.Column(projectId, type, column.Id, false), itemId, Slot.Last, null, cancellationToken));
    }
}
