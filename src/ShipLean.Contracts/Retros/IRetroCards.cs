namespace ShipLean.Contracts.Retros;

// The retros as cards, in the order asked for; archived points are left out. Every view that shows cards loads them here.
public interface IRetroCards
{
    Task<IReadOnlyList<RetroCardDto>> LoadAsync(IReadOnlyList<Guid> retroIds, CancellationToken cancellationToken);
}
