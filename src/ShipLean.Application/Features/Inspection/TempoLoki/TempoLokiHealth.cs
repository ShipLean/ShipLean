using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

public sealed class TempoLokiHealth
{
    // TODO: ping the store and report latency and view lag (lane B health check).
    // Instance method so the lane can add dependencies to this part; S2325 clears when it does (it uses no state in the stub).
#pragma warning disable S2325
    public Task<StoreHealth> CheckAsync(CancellationToken ct) => Task.FromResult(new StoreHealth(false, null, null, "The store health check is not built yet."));
#pragma warning restore S2325
}
