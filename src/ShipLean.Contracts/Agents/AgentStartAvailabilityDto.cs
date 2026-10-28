namespace ShipLean.Contracts.Agents;

// Whether Start agent works on an item. Reason is null when it does; Message then reads for a person ("Choose an agent connection
// in the project's General settings.") and is empty.
public sealed record AgentStartAvailabilityDto(bool CanStart, AgentStartBlock? Reason, string Message);
