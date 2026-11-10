using ShipLean.Contracts.Screens;

namespace ShipLean.Application.Features.Backlog.NeedsYou;

public sealed class NeedsYouCounts : INeedsYouCounts
{
    // TODO: count SHIP-922's open questions and decisions on the asked items AskOwnerSql.OwnedByUser matches for the user, rolling
    // Story asks up to their Feature (OnChildren). Until then nothing waits on anyone.
    public Task<IReadOnlyDictionary<Guid, NeedsYouDto>> ForItemsAsync(Guid userId, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<Guid, NeedsYouDto>>(new Dictionary<Guid, NeedsYouDto>());
}
