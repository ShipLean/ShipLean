using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

internal static class ClickHouseHealth
{
    // TODO: ping the store and report latency and view lag (lane A health check).
    public static Task<StoreHealth> CheckAsync(CancellationToken ct) => Task.FromResult(new StoreHealth(false, null, null, "The store health check is not built yet."));
}
