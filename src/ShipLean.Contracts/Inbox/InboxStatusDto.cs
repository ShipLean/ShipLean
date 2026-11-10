namespace ShipLean.Contracts.Inbox;

// A Feature's status line to check. VerifiedAt is null when it was never verified.
public sealed record InboxStatusDto(string StatusLine, DateTimeOffset? VerifiedAt);
