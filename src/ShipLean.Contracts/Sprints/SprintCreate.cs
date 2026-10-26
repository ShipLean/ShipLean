namespace ShipLean.Contracts.Sprints;

public sealed record SprintCreate(string Name, DateOnly StartDate, DateOnly EndDate, string Goal);
