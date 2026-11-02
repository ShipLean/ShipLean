namespace ShipLean.Contracts.Usage;

// POST api/usage/screen: the SPA reports that a screen was opened.
public sealed record ScreenViewDto(string Screen, Guid? ProjectId);
