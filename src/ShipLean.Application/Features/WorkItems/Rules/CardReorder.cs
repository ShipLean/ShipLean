using System.Data.Common;
using Dapper;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItems.Rules;

// With Card reordering set to change the backlog order, a card dropped within or into a column takes its backlog place next to its column
// neighbours: just above the card that follows it in the column, or just below the one before it when it is last.
internal static class CardReorder
{
    private const string NextPeer =
        """
        SELECT id, backlog_order AS BacklogOrder FROM work_item
        WHERE project_id = @ProjectId AND board_column_id = @ColumnId AND board_column_done = @Done AND column_order IS NOT NULL
          AND type = ANY(@types) AND id <> @id AND column_order > @order
        ORDER BY column_order, id LIMIT 1
        """;

    private const string PreviousPeer =
        """
        SELECT id, backlog_order AS BacklogOrder FROM work_item
        WHERE project_id = @ProjectId AND board_column_id = @ColumnId AND board_column_done = @Done AND column_order IS NOT NULL
          AND type = ANY(@types) AND id <> @id AND column_order < @order
        ORDER BY column_order DESC, id DESC LIMIT 1
        """;

    private const string BacklogBefore =
        "SELECT backlog_order FROM work_item WHERE project_id = @projectId AND id <> @id AND backlog_order < @key ORDER BY backlog_order DESC LIMIT 1";

    private const string BacklogAfter =
        "SELECT backlog_order FROM work_item WHERE project_id = @projectId AND id <> @id AND backlog_order > @key ORDER BY backlog_order LIMIT 1";

    public static async Task<ProposedChange> ApplyAsync(
        WorkItemSnapshot before, ProposedChange proposed, ProposedChange amended, BoardSettingsJson board, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var dropped = proposed.ColumnId is not null && proposed.ColumnOrder is not null
            && (proposed.ColumnId != before.ColumnId || proposed.ColumnDone != before.ColumnDone || proposed.ColumnOrder != before.ColumnOrder);
        if (board.CardReordering != CardReordering.ChangesBacklogOrder || !dropped || amended.ColumnId != proposed.ColumnId)
        {
            return amended;
        }

        var types = Enum.GetValues<ShipLean.Contracts.Refs.WorkItemTypeId>().Where(t => WorkItemTypes.BoardLevel(t) == WorkItemTypes.BoardLevel(proposed.Type)).Select(WorkItemTypes.Db).ToArray();
        var parameters = new { proposed.ProjectId, proposed.ColumnId, Done = proposed.ColumnDone, types, id = proposed.WorkItemId, order = proposed.ColumnOrder };
        var connection = transaction.Connection!;
        var next = await connection.QuerySingleOrDefaultAsync<PeerRow>(new CommandDefinition(NextPeer, parameters, transaction, cancellationToken: cancellationToken));
        string? lower;
        string? upper;
        if (next is not null)
        {
            upper = next.BacklogOrder;
            lower = await NeighbourAsync(connection, transaction, proposed, BacklogBefore, upper, cancellationToken);
        }
        else if (await connection.QuerySingleOrDefaultAsync<PeerRow>(new CommandDefinition(PreviousPeer, parameters, transaction, cancellationToken: cancellationToken)) is { } previous)
        {
            lower = previous.BacklogOrder;
            upper = await NeighbourAsync(connection, transaction, proposed, BacklogAfter, lower, cancellationToken);
        }
        else
        {
            return amended;
        }

        return TryBetween(lower, upper) is { } key ? amended with { BacklogOrder = key } : amended;
    }

    // The backlog key next to the given one (excluding the card itself), in the given direction.
    private static Task<string?> NeighbourAsync(System.Data.Common.DbConnection connection, DbTransaction transaction, ProposedChange card, string sql, string key, CancellationToken cancellationToken)
        => connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            sql,
            new { projectId = card.ProjectId, id = card.WorkItemId, key }, transaction, cancellationToken: cancellationToken));

    // No key when the neighbours leave no room or the key would be longer than the store writes: the backlog order then stays as it was and the
    // card keeps its place in the column only. The rules write nothing themselves, so they cannot renumber the backlog the way the store's own
    // ordering does when keys run out; renumbering when a rule returns no key is a later follow-up in the store.
    private static string? TryBetween(string? lower, string? upper)
    {
        try
        {
            var key = Rank.Between(lower, upper);
            return key.Length <= WorkItemOrdering.MAX_KEY_LENGTH ? key : null;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private sealed record PeerRow(Guid Id, string BacklogOrder);
}
