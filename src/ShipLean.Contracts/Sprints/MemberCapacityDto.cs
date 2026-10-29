using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Sprints;

// Days is what was typed (null: the sprint's DefaultDays). Points is the stored capacity: computed from the sprint's velocity
// and the days when the sprint has a velocity, else the old points-only figure.
public sealed record MemberCapacityDto(UserRef User, decimal? Days, decimal Points, string? Note);
