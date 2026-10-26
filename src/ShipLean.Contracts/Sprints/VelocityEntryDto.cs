namespace ShipLean.Contracts.Sprints;

public sealed record VelocityEntryDto(Guid SprintId, string SprintName, decimal Committed, decimal Completed);
