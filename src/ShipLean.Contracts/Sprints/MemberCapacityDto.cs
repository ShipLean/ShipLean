using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Sprints;

public sealed record MemberCapacityDto(UserRef User, decimal Points, string? Note);
