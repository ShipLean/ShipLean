namespace ShipLean.Application.Features.InspectionMetrics.Quality;

// A pull request linked to a live item of the project (a url link with a link_state row) that merged at MergedAt (link_state.merged_at).
// A PR linked to several items of the project is one fact, keyed by its url.
public sealed record MergedPrFact(string Url, DateTimeOffset MergedAt);
