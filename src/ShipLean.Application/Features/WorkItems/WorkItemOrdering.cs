using System.Globalization;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItems;

// Where an order key is written: the key is compared only with the keys of the same scope.
internal enum OrderKind
{
    Backlog,
    Column,
    Cell,
}

// The set of cards one order key is placed among: the project's backlog (every item, Removed included, so a restore never
// collides), one Doing or Done half of a board column at one level, or one Taskboard cell (the tasks of one state under one parent).
internal sealed record OrderScope(OrderKind Kind, Guid ProjectId, string? ColumnId, bool Done, Guid? StateId, Guid? ParentId, string? Level)
{
    public static OrderScope Backlog(Guid projectId) => new(OrderKind.Backlog, projectId, null, false, null, null, null);

    public static OrderScope Column(Guid projectId, WorkItemTypeId type, string columnId, bool done)
        => new(OrderKind.Column, projectId, columnId, done, null, null, WorkItemTypes.BoardLevel(type));

    // The scope the item's column order belongs to, or null when it has none (an item off its board).
    public static OrderScope? Of(ProposedChange change)
    {
        if (change.ColumnId is { } column)
        {
            return Column(change.ProjectId, change.Type, column, change.ColumnDone);
        }

        return WorkItemTypes.BoardLevel(change.Type) is null ? new(OrderKind.Cell, change.ProjectId, null, false, change.StateId, change.ParentId, null) : null;
    }
}

// Where in its scope a card goes.
internal enum Slot
{
    Last,
    First,
    Before,

    // Before the given card when it is in the scope, last otherwise (the card a rule moved to another scope).
    BeforeOrLast,
}

// The order keys a create, reorder or move writes: between the card before the target and the target, or after the last card.
// Peers are ordered `NULLS LAST, id`, the order the Taskboard reads. Callers hold the project's write lock, so the peers they read are the committed ones.
internal static class WorkItemOrdering
{
    // A key longer than this is not written: the scope is renumbered first.
    public const int MAX_KEY_LENGTH = 24;

    private const long SPACE = 1_000_000_000_000;

    private const string BacklogPeers =
        "SELECT id, backlog_order AS orderKey FROM work_item WHERE project_id = @ProjectId AND id <> @itemId ORDER BY backlog_order, id";

    private const string ColumnPeers =
        """
        SELECT id, column_order AS orderKey FROM work_item
        WHERE project_id = @ProjectId AND board_column_id = @ColumnId AND board_column_done = @Done
          AND type = ANY(@types) AND id <> @itemId
        ORDER BY column_order NULLS LAST, id
        """;

    private const string CellPeers =
        """
        SELECT id, column_order AS orderKey FROM work_item
        WHERE project_id = @ProjectId AND board_column_id IS NULL AND state_id = @StateId AND parent_id IS NOT DISTINCT FROM @ParentId
          AND type = 'task' AND id <> @itemId
        ORDER BY column_order NULLS LAST, id
        """;

    private const string RenumberBacklog = "UPDATE work_item w SET backlog_order = v.key FROM unnest(@ids, @keys) AS v(id, key) WHERE w.id = v.id";

    private const string RenumberColumn = "UPDATE work_item w SET column_order = v.key FROM unnest(@ids, @keys) AS v(id, key) WHERE w.id = v.id";

    public static async Task<string> KeyAsync(WriteScope scope, OrderScope orderScope, Guid itemId, Slot slot, Guid? beforeItemId, CancellationToken cancellationToken)
    {
        var peers = await PeersAsync(scope, orderScope, itemId, cancellationToken);
        if (peers.Any(p => p.OrderKey is null))
        {
            // Unkeyed cards (created through the API, or seeded) are peers too, after the keyed ones: give the scope keys in that order first.
            peers = await RenumberAsync(scope, orderScope, peers, cancellationToken);
        }

        var index = IndexOf(peers, slot, beforeItemId);
        var key = TryBetween(peers, index);
        if (key is { Length: <= MAX_KEY_LENGTH })
        {
            return key;
        }

        peers = await RenumberAsync(scope, orderScope, peers, cancellationToken);
        return Between(peers, index);
    }

    // The key between the neighbours, or null when they leave no room (equal or out of order: a tie some earlier write left).
    private static string? TryBetween(IReadOnlyList<Peer> peers, int index)
    {
        try
        {
            return Between(peers, index);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    // The slot as an index into the peers: the card the new key goes in front of, peers.Count for the back.
    private static int IndexOf(IReadOnlyList<Peer> peers, Slot slot, Guid? beforeItemId)
    {
        switch (slot)
        {
            case Slot.Last:
                return peers.Count;
            case Slot.First:
                return 0;
        }

        var found = beforeItemId is { } before ? peers.ToList().FindIndex(p => p.Id == before) : -1;
        if (found >= 0)
        {
            return found;
        }

        return slot == Slot.BeforeOrLast ? peers.Count : throw ApiException.NotFound("The item to place it before isn't there.");
    }

    private static string Between(IReadOnlyList<Peer> peers, int index)
        => Rank.Between(index == 0 ? null : peers[index - 1].OrderKey, index == peers.Count ? null : peers[index].OrderKey);

    private static async Task<IReadOnlyList<Peer>> PeersAsync(WriteScope scope, OrderScope orderScope, Guid itemId, CancellationToken cancellationToken)
    {
        var sql = orderScope.Kind switch
        {
            OrderKind.Backlog => BacklogPeers,
            OrderKind.Column => ColumnPeers,
            _ => CellPeers,
        };
        var types = Enum.GetValues<WorkItemTypeId>().Where(t => WorkItemTypes.BoardLevel(t) == orderScope.Level).Select(WorkItemTypes.Db).ToArray();
        var parameters = new DynamicParameters(orderScope);
        parameters.Add("itemId", itemId);
        parameters.Add("types", types);
        var peers = await scope.Connection.QueryAsync<Peer>(new CommandDefinition(
            sql, parameters, scope.Transaction, cancellationToken: cancellationToken));
        return [.. peers];
    }

    // Spreads the scope's cards over evenly spaced 12-digit keys in their current order. Position only: no revision, no event.
    private static async Task<IReadOnlyList<Peer>> RenumberAsync(WriteScope scope, OrderScope orderScope, IReadOnlyList<Peer> peers, CancellationToken cancellationToken)
    {
        var step = SPACE / (peers.Count + 2);
        var renumbered = peers.Select((p, i) => p with { OrderKey = ((i + 1) * step).ToString("D12", CultureInfo.InvariantCulture) }).ToList();
        await scope.Connection.ExecuteAsync(new CommandDefinition(
            orderScope.Kind == OrderKind.Backlog ? RenumberBacklog : RenumberColumn,
            new { ids = renumbered.Select(p => p.Id).ToArray(), keys = renumbered.Select(p => p.OrderKey).ToArray() },
            scope.Transaction,
            cancellationToken: cancellationToken));
        return renumbered;
    }

    private sealed record Peer(Guid Id, string? OrderKey);
}
