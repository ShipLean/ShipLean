namespace ShipLean.Application.Features.Inbox;

// A live project member whose full name a comment can @mention.
public sealed record MentionCandidate(Guid UserId, string Name);
