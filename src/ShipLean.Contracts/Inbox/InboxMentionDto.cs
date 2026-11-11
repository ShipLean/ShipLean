namespace ShipLean.Contracts.Inbox;

// An open @mention on an Inbox row. Excerpt is the comment as plain text, at most InboxRules.ExcerptLength characters, cut
// on a word with "…".
public sealed record InboxMentionDto(Guid MentionId, Guid CommentId, string Excerpt);
