using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

internal static class TempoLokiRunFinder
{
    // TODO: find the runs linked to the item through the store (lane B finder), applying LinkRules, the project filter and the time window.
    public static Task<RunSearch> FindAsync(RunQuery query, CancellationToken ct) => Task.FromResult(new RunSearch([], true, null));
}
