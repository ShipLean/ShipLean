namespace ShipLean.Contracts.Sprints;

// One sprint in the forecast. SprintId is null for a hypothetical sprint past the last real one (named "Sprint 27").
// Room is what was free before the forecast filled it: the team's capacity when entered (FromCapacity), else the velocity, minus the points already in the sprint.
// LastItemId is the last backlog item forecast into it (the cut line; null when none); Points counts unestimated items as zero.
public sealed record ForecastSprintDto(
    Guid? SprintId, string Name, decimal Room, bool FromCapacity, Guid? LastItemId, int ItemCount, decimal Points, int UnestimatedCount);
