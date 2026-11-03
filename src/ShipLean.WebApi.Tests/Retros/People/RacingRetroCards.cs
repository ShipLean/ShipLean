using ShipLean.Contracts.Retros;

namespace ShipLean.WebApi.Tests.Retros.People;

// The real cards, with a hook that runs just before they are read: stands in for another request that changes a link between a reader's two reads.
public sealed class RacingRetroCards(IRetroCards inner) : IRetroCards
{
    public Func<Task>? BeforeLoad { get; set; }

    public async Task<IReadOnlyList<RetroCardDto>> LoadAsync(IReadOnlyList<Guid> retroIds, CancellationToken cancellationToken)
    {
        if (BeforeLoad is { } hook)
        {
            BeforeLoad = null;
            await hook();
        }
        return await inner.LoadAsync(retroIds, cancellationToken);
    }
}
