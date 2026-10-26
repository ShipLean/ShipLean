namespace ShipLean.Application.Fakes.WorkItems;

// Who wrote a revision, when, and whether it came from an import.
internal sealed record InMemoryStamp(Guid By, DateTimeOffset At, string? OriginalSystem, Guid? ImportJobId, string? AgentName = null, string? Via = null);
