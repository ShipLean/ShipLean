namespace ShipLean.Contracts.Links;

// Reads again the live state and previews of the item's cards that are due, then answers with the item's links.
public interface ILinkCardsRefresh
{
    Task<LinkCardsDto> RefreshAsync(Guid workItemId, CancellationToken cancellationToken = default);
}
