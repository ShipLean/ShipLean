namespace ShipLean.Contracts.Screens;

// The phone's Features overview. SinceLabel is made by the server ("since yesterday", "since Mon 09:12"); ChangedSince counts the
// Features changed since then. Groups come Active, Resolved, New, then Closed (folded).
public sealed record FeaturesOverviewDto(Guid ProjectId, string SinceLabel, int ChangedSince, IReadOnlyList<FeatureGroupDto> Groups);
