namespace ShipLean.Contracts.Screens;

// What changed on a project's Features since a moment, newest first. NeedsYou holds the items waiting on the caller; Changed the rest.
// Cursor fetches the next page when More is true.
public sealed record FeatureDigestDto(
    DateTimeOffset Since,
    string SinceLabel,
    IReadOnlyList<DigestItemDto> NeedsYou,
    IReadOnlyList<DigestItemDto> Changed,
    string? Cursor,
    bool More);
