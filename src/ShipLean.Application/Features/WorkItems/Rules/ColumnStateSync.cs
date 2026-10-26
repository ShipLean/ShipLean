using System.Data.Common;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems.Rules;

// Keeps an item's board column and state in step on the board of its level (the Taskboard has no columns on the item: its columns are states).
internal static class ColumnStateSync
{
    public static async Task<ProposedChange> ApplyAsync(
        WorkItemSnapshot before, ProposedChange change, BoardSettingsJson board, IReadOnlyDictionary<Guid, RuleState> states, DbTransaction transaction, List<string> notices, CancellationToken cancellationToken)
    {
        var columnChanged = change.ColumnId is not null && change.ColumnId != before.ColumnId;
        var stateChanged = change.StateId != before.StateId;
        if (columnChanged)
        {
            return await ColumnWinsAsync(change, board, stateChanged, states, transaction, notices, cancellationToken);
        }

        return stateChanged ? await StateWinsAsync(change, board, states, transaction, notices, cancellationToken) : change;
    }

    // The column was set: the state becomes the column's state for the type (the first one when it maps several). A caller's state that
    // the column maps too stays. A column that maps nothing for the type leaves the state alone.
    private static async Task<ProposedChange> ColumnWinsAsync(
        ProposedChange change, BoardSettingsJson board, bool stateChanged, IReadOnlyDictionary<Guid, RuleState> states, DbTransaction transaction, List<string> notices, CancellationToken cancellationToken)
    {
        var mapped = board.Columns.First(c => c.Id == change.ColumnId).StateMappings.Where(m => m.Type == change.Type).Select(m => m.StateId).ToList();
        if (mapped.Count == 0 || mapped.Contains(change.StateId))
        {
            return change;
        }

        var name = (await NamesAsync(transaction, states, mapped[0], cancellationToken)).Name;
        notices.Add(stateChanged ? $"Kept the column: state set to {name}" : $"State set to {name}");
        return change with { StateId = mapped[0] };
    }

    // The state was set: the card goes to the first column that maps it, or off the board when none does. A card whose column
    // already maps the new state stays where it is.
    private static async Task<ProposedChange> StateWinsAsync(
        ProposedChange change, BoardSettingsJson board, IReadOnlyDictionary<Guid, RuleState> states, DbTransaction transaction, List<string> notices, CancellationToken cancellationToken)
    {
        bool Maps(BoardColumnJson column) => column.StateMappings.Any(m => m.Type == change.Type && m.StateId == change.StateId);
        if (change.ColumnId is { } current && board.Columns.FirstOrDefault(c => c.Id == current) is { } here && Maps(here))
        {
            return change;
        }

        if (board.Columns.FirstOrDefault(Maps) is { } column)
        {
            notices.Add($"Moved to {column.Name}");
            var lane = change.LaneId ?? board.Swimlanes.FirstOrDefault(l => l.Kind == SwimlaneKind.Default)?.Id;
            return change with { ColumnId = column.Id, ColumnDone = false, LaneId = lane };
        }

        if (change.ColumnId is null)
        {
            return change;
        }

        var name = (await NamesAsync(transaction, states, change.StateId, cancellationToken)).Name;
        notices.Add($"Left the board: no column maps {name}");
        return change with { ColumnId = null, ColumnDone = false, LaneId = null, ColumnOrder = null };
    }

    private static async Task<RuleState> NamesAsync(DbTransaction transaction, IReadOnlyDictionary<Guid, RuleState> known, Guid id, CancellationToken cancellationToken)
        => known.TryGetValue(id, out var state) ? state : (await RuleReads.StatesAsync(transaction, [id], cancellationToken))[id];
}
