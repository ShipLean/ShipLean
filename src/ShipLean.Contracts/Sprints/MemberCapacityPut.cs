namespace ShipLean.Contracts.Sprints;

public sealed record MemberCapacityPut(Guid UserId, decimal Points, string? Note);
