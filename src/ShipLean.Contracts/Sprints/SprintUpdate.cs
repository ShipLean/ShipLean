namespace ShipLean.Contracts.Sprints;

// Null leaves a field as it is.
public sealed record SprintUpdate(string? Name, DateOnly? StartDate, DateOnly? EndDate, string? Goal);
