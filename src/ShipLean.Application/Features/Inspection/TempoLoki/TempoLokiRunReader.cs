using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

public sealed class TempoLokiRunReader
{
    // TODO: read one run's spans and logs from the store, normalised as K3 says (lane B reader); null unless the run links to the item.
    // Instance method so the lane can add dependencies to this part; the stub returns a field so S2325 stays quiet without a suppression.
    private readonly Task<RawRun?> notBuilt = Task.FromResult<RawRun?>(null);
    public Task<RawRun?> GetAsync(RunRef run, CancellationToken ct) => notBuilt;
}
