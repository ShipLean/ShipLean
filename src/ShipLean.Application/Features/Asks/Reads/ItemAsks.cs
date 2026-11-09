using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Reads;

// TODO: the reader over the decision and question tables replaces this stub, which answers every item with no asks.
public sealed class ItemAsks : IItemAsks
{
    public Task<ItemAsksDto> GetAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => Task.FromResult(new ItemAsksDto(workItemId, null, false, false, 0, 0, [], [], []));
}
