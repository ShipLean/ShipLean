namespace ShipLean.Contracts.Inbox;

// The body of POST api/mentions/{id}/reply. Body is Markdown; the server prefixes "@<mentioner's full name>, ".
public sealed record MentionReplyRequest(string Body);
