using ShipLean.Application.Features.InspectionMetrics.Facts;

namespace ShipLean.Application.Features.InspectionMetrics.Quality;

// Everything the quality aggregator reads (QK4): the project's bugs, its merged PRs, its severity weights (severity 1-4 to weight,
// project.bug_severity_weights, item D16) and Causes, the lead-time facts (K5 shapes) of every Task a bug's Caused by link points
// at. The aggregator measures each cause with ITaskLeadTimeMeasure to find the sprint the bug counts in (design D11); the facts
// carry no precomputed end.
public sealed record BugFacts(
    IReadOnlyList<BugFact> Bugs,
    IReadOnlyList<MergedPrFact> MergedPrs,
    IReadOnlyDictionary<int, int> SeverityWeights,
    LeadTimeFacts Causes);
