using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Screens;

// One change in the digest. Text is made by the server; By is the person and AgentName the agent stamp when an agent made it.
public sealed record DigestLineDto(DigestLineKind Kind, string Text, UserRef? By, string? AgentName, DateTimeOffset At, string AtLabel);
