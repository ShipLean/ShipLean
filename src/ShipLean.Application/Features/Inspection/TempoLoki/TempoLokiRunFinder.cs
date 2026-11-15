using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

public sealed class TempoLokiRunFinder
{
    // TODO: find the runs linked to the item through the store (lane B finder), applying LinkRules, the project filter and the time window.
    // Instance method so the lane can add dependencies to this part; S2325 clears when it does (it uses no state in the stub).
#pragma warning disable S2325
    public Task<RunSearch> FindAsync(RunQuery query, CancellationToken ct) => Task.FromResult(new RunSearch([], true, null));
#pragma warning restore S2325
}
