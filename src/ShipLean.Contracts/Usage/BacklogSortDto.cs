namespace ShipLean.Contracts.Usage;

// POST api/projects/{id}/usage/backlog-sort: the SPA reports a click on a backlog column title in list mode. Screen is the backlog's
// route id, Column the column id, Direction the sort it led to: asc, desc, or off (back to rank order).
public sealed record BacklogSortDto(string Screen, string Column, string Direction);
