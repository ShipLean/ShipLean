using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

public sealed class ClickHouseRunFinder
{
    // TODO: find the runs linked to the item through the store (lane A finder), applying LinkRules, the project filter and the time window.
    // Instance method so the lane can add dependencies to this part; the stub returns a field so S2325 stays quiet without a suppression.
    private readonly Task<RunSearch> notBuilt = Task.FromResult(new RunSearch([], true, null));
    public Task<RunSearch> FindAsync(RunQuery query, CancellationToken ct) => notBuilt;
}
