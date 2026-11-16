namespace ShipLean.Contracts.Views.Timeline;

// The time between two key dates. Days and Label are null while From isn't reached; when From is reached and To isn't, the gap
// runs to AsOf and SoFar is true.
public sealed record TimelineGapDto(KeyDatePoint From, KeyDatePoint To, double? Days, string? Label, bool SoFar);
