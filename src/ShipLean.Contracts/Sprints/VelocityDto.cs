namespace ShipLean.Contracts.Sprints;

// Sprints are the last Window ended sprints, oldest first; Average is their mean Completed; Override replaces it when set.
public sealed record VelocityDto(int Window, IReadOnlyList<VelocityEntryDto> Sprints, decimal Average, decimal? Override);
