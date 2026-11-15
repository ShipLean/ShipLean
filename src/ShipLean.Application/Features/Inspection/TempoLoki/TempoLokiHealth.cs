using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

internal static class TempoLokiHealth
{
    // TODO: ping the store and report latency and view lag (lane B health check).
    public static Task<StoreHealth> CheckAsync(CancellationToken ct) => Task.FromResult(new StoreHealth(false, null, null, "The store health check is not built yet."));
}
