namespace ShipLean.Contracts.Sprints;

public sealed record BurndownPoint(DateOnly Date, decimal Remaining, decimal Ideal);
