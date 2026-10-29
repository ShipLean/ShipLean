namespace ShipLean.Contracts.Sprints;

// Days null means the sprint's default (working days minus holidays); otherwise 0..WorkingDays in steps of 0.5. The server computes the points.
public sealed record MemberCapacityPut(Guid UserId, decimal? Days, string? Note);
