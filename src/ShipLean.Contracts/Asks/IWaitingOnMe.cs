namespace ShipLean.Contracts.Asks;

// Open asks on the items the signed-in person owns (coalesce(assigned_to, created_by) = me), in projects they are a live
// member of. Removed or archived items and archived projects are left out.
public interface IWaitingOnMe
{
    Task<WaitingDto> GetAsync(CancellationToken cancellationToken = default);
}
