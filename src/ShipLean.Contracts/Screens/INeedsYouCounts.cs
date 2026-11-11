namespace ShipLean.Contracts.Screens;

// The open questions and decisions waiting on a user, per work item: SHIP-922's open asks on the items AskOwnerSql.OwnedByUser
// matches, with a Story's asks rolled up to its Feature (OnChildren). An item with nothing waiting is left out of the answer.
// Callers check access first.
public interface INeedsYouCounts
{
    Task<IReadOnlyDictionary<Guid, NeedsYouDto>> ForItemsAsync(Guid userId, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken);
}
