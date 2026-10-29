namespace ShipLean.Contracts.Sprints;

// Velocity (0..9999.99, two decimals) is required when Members is not empty; with no members it is optional and, when
// sent, still stored. A PUT replaces the entered set.
public sealed record SprintCapacityPut(IReadOnlyList<MemberCapacityPut> Members, decimal? Velocity = null);
