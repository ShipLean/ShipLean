namespace ShipLean.Contracts.Sprints;

// Members lists only the entered rows (a member with no entry is absent, and the sprint falls back to velocity); TeamTotal is the sum of their Points.
// The capacity screen reads velocity for comparison from ISprintMetrics.
public sealed record SprintCapacityDto(Guid SprintId, IReadOnlyList<MemberCapacityDto> Members, decimal TeamTotal);
