namespace ShipLean.Application.Features.Timeline.Facts;

// Who made a change: the agent when AgentName is set, else the user UserId (either may be null when the history doesn't say).
public sealed record TimelineActorFact(Guid? UserId, string? AgentName);
