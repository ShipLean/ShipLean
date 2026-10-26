using Dapper;
using System.Data.Common;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using System.Text.Json;

namespace ShipLean.Application.Features.WorkItems.Rules;

// What the rules read through the write's transaction: states (name and category) and the board of an item's level.
internal static class RuleReads
{
    public static async Task<IReadOnlyDictionary<Guid, RuleState>> StatesAsync(DbTransaction transaction, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var rows = await transaction.Connection!.QueryAsync<RuleState>(new CommandDefinition(
            "SELECT id, name, category FROM state WHERE id = ANY(@ids)",
            new { ids = ids.Distinct().ToArray() }, transaction, cancellationToken: cancellationToken));
        return rows.ToDictionary(s => s.Id);
    }

    // The settings of the board that shows the type, read FOR SHARE like every card placement (a board settings put holds the row FOR UPDATE,
    // so a card cannot land in a column the put is removing), or null when the type has none (tasks) or the project has no such board.
    public static async Task<BoardSettingsJson?> BoardAsync(DbTransaction transaction, Guid projectId, WorkItemTypeId type, CancellationToken cancellationToken)
    {
        if (WorkItemTypes.BoardLevel(type) is not { } level)
        {
            return null;
        }

        var json = await transaction.Connection!.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT settings::text FROM board WHERE project_id = @projectId AND level = @level FOR SHARE",
            new { projectId, level }, transaction, cancellationToken: cancellationToken));
        return json is null ? null : JsonSerializer.Deserialize<BoardSettingsJson>(json, PersistedJson.Options);
    }
}
