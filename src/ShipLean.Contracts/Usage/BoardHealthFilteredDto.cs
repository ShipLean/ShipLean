namespace ShipLean.Contracts.Usage;

// POST api/projects/{id}/usage/board-health-filtered: the SPA reports the Health filter of the Features board changed. Health is
// what it was changed to: onTrack, atRisk, blocked, none (Not set) or any (the filter cleared).
public sealed record BoardHealthFilteredDto(string Health);
