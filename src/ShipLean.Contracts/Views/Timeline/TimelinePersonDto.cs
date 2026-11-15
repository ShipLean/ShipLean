namespace ShipLean.Contracts.Views.Timeline;

// Who did something: an agent (Agent true, Name = the revision's agent name) or a person (UserId set). UserId is null for an agent.
public sealed record TimelinePersonDto(Guid? UserId, string Name, bool Agent);
