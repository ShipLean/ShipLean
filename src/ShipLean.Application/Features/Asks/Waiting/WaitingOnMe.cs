using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Waiting;

// TODO: the waiting-on-me reader over the decision and question tables replaces this stub.
public sealed class WaitingOnMe : IWaitingOnMe
{
    public Task<WaitingDto> GetAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
