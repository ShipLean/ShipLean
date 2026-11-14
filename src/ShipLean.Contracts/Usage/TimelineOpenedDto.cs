namespace ShipLean.Contracts.Usage;

// POST api/projects/{id}/usage/timeline-opened: the SPA reports a Feature timeline opened. From is where it was opened from:
// feature (Open timeline on the Feature page, which adds ?from=feature) or link (anything else).
public sealed record TimelineOpenedDto(string From);
