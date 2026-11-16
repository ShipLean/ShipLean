namespace ShipLean.Contracts.Agents;

// Whether Start agent works on an item. Reason is null when it does; Message then reads for a person ("Choose an agent connection
// in the project's General settings.") and is empty. Warning is IAgentConnectionWarnings' text for the project's connection, or null.
public sealed record AgentStartAvailabilityDto(bool CanStart, AgentStartBlock? Reason, string Message, string? Warning);
