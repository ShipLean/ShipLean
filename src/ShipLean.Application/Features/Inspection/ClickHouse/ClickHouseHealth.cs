using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

public sealed class ClickHouseHealth
{
    // TODO: ping the store and report latency and view lag (lane A health check).
    // Instance method so the lane can add dependencies to this part; the stub returns a field so S2325 stays quiet without a suppression.
    private readonly Task<StoreHealth> notBuilt = Task.FromResult(new StoreHealth(false, null, null, "The store health check is not built yet."));
    public Task<StoreHealth> CheckAsync(CancellationToken ct) => notBuilt;
}
