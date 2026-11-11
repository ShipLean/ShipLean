using Agoda.IoC.Core;
using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Waiting;

// What the waiting endpoint does. Any signed-in person reads their own list; it only ever holds items of projects they are in.
[RegisterPerRequest]
public sealed class WaitingEndpoints(IWaitingOnMe waiting)
{
    public Task<WaitingDto> GetAsync(CancellationToken cancellationToken) => waiting.GetAsync(cancellationToken);
}
