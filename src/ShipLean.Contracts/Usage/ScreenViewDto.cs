namespace ShipLean.Contracts.Usage;

// POST api/usage/screen: the SPA reports that a screen was opened. Mode is the backlog mode (list, planning or tree) on a backlog
// screen, null elsewhere.
public sealed record ScreenViewDto(string Screen, Guid? ProjectId, string? Mode = null);
