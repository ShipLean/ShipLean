using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Inbox;

// What happened next on an answered item. By is the person (for an agent, the person it runs for) and AgentName the agent,
// or null when a person did it.
public sealed record AnsweredNextDto(AnsweredNextKind Kind, UserRef? By, string? AgentName, DateTimeOffset At, string AtLabel);
