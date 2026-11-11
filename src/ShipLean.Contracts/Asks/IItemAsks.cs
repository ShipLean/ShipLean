namespace ShipLean.Contracts.Asks;

// Reads a work item's decisions and questions. Trusts its caller: the endpoint or ItemResolver checks access at Member first.
public interface IItemAsks
{
    // Throws NotFound for an unknown work item.
    Task<ItemAsksDto> GetAsync(Guid workItemId, CancellationToken cancellationToken = default);
}
