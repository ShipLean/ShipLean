using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

internal static class ClickHouseRunReader
{
    // TODO: read one run's spans and logs from the store, normalised as K3 says (lane A reader); null unless the run links to the item.
    public static Task<RawRun?> GetAsync(RunRef run, CancellationToken ct) => Task.FromResult<RawRun?>(null);
}
