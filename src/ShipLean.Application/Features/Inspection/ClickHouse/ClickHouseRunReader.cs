using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

public sealed class ClickHouseRunReader
{
    // TODO: read one run's spans and logs from the store, normalised as K3 says (lane A reader); null unless the run links to the item.
    // Instance method so the lane can add dependencies to this part; S2325 clears when it does (it uses no state in the stub).
#pragma warning disable S2325
    public Task<RawRun?> GetAsync(RunRef run, CancellationToken ct) => Task.FromResult<RawRun?>(null);
#pragma warning restore S2325
}
