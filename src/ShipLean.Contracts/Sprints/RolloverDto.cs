using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Sprints;

// NextSprintCapacity is the next sprint's entered team capacity (null when none); NextSprintPoints is what is already planned into it.
public sealed record RolloverDto(
    SprintRef Sprint, RolloverStatus Status, IReadOnlyList<RolloverItemDto> Items, SprintRef? NextSprint, decimal? NextSprintCapacity, decimal NextSprintPoints);
