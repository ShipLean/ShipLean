namespace ShipLean.Application.Features.InspectionMetrics.Quality;

// The quality facts of a project (QK4), over the same days as the lead-time read (K5) and with the same UTC edges: (fromDay - 1 day)
// 00:00Z up to (toDay + 3 days) 00:00Z. Bugs: every live Bug filed in that span, found before release ones included. MergedPrs:
// every PR linked to a live item of the project that merged in that span. SeverityWeights: the project's, always all four.
// Causes: the lead-time facts of each Task a returned bug's live Caused by link points at, whatever their dates. AnyBugs: whether a
// live Bug exists in the project at all (an EXISTS, whatever its date).
public interface IQualityFactsReader
{
    Task<BugFacts> ReadAsync(Guid projectId, DateOnly fromDay, DateOnly toDay, CancellationToken ct);
}
