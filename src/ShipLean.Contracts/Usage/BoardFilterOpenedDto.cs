namespace ShipLean.Contracts.Usage;

// POST api/projects/{id}/usage/board-filter-opened: the SPA reports a board's Filter control opened. Level is the board it was
// opened on: features, stories or epics.
public sealed record BoardFilterOpenedDto(string Level);
