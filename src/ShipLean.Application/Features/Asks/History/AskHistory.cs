using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.History;

// TODO: the ask history over the ask_event table, with same-moment raises folded, replaces this stub.
public sealed class AskHistory : IAskHistory
{
    public Task<IReadOnlyList<AskHistoryLineDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
