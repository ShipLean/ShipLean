using System.Globalization;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Fakes.WorkItems;

// Backlog order and column order as sortable decimal strings (the fake's stand-in for ranks). The store computes the
// order for a write before the rules run and writes back whatever the rules return, so nothing here renumbers other items.
internal static class InMemoryOrder
{
    private const decimal START = 500_000_000_000m;
    private const decimal STEP = 1_000m;

    // The order that puts an item before beforeItemId, last when null, or first when atTop.
    public static string BacklogKey(InMemoryWorld world, Guid projectId, Guid itemId, Guid? beforeItemId, bool atTop)
    {
        var peers = world.Items.Values
            .Where(i => i.ProjectId == projectId && i.Id != itemId)
            .OrderBy(i => i.BacklogOrder, StringComparer.Ordinal)
            .ToList();
        return KeyIn(peers, i => i.BacklogOrder, atTop ? peers.FirstOrDefault()?.Id : beforeItemId);
    }

    // The order within the item's column, or within its (state, parent) cell when it has no column (the Taskboard).
    public static string ColumnKey(InMemoryWorld world, ProposedChange change, Guid? beforeItemId)
    {
        var peers = world.Items.Values
            .Where(i => i.ProjectId == change.ProjectId && i.Id != change.WorkItemId && i.ColumnOrder is not null)
            .Where(i => change.ColumnId is not null
                ? i.ColumnId == change.ColumnId
                : i.ColumnId is null && i.StateId == change.StateId && i.ParentId == change.ParentId)
            .OrderBy(i => i.ColumnOrder, StringComparer.Ordinal)
            .ToList();
        return KeyIn(peers, i => i.ColumnOrder!, beforeItemId);
    }

    private static string KeyIn(IReadOnlyList<WorkItemSnapshot> peers, Func<WorkItemSnapshot, string> order, Guid? beforeItemId)
    {
        if (beforeItemId is not { } before)
        {
            return Format(peers.Count == 0 ? START : Parse(order(peers[^1])) + STEP);
        }

        var index = peers.ToList().FindIndex(i => i.Id == before);
        if (index < 0)
        {
            throw ApiException.NotFound("The item to place it before isn't there.");
        }

        var next = Parse(order(peers[index]));
        if (index == 0)
        {
            return Format(next - STEP);
        }

        return Format((Parse(order(peers[index - 1])) + next) / 2);
    }

    private static decimal Parse(string order) => decimal.Parse(order, CultureInfo.InvariantCulture);

    private static string Format(decimal value) => value.ToString("000000000000.0000000000000000", CultureInfo.InvariantCulture);
}
