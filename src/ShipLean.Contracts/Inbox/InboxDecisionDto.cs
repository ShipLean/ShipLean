namespace ShipLean.Contracts.Inbox;

// An open decision on an Inbox row.
public sealed record InboxDecisionDto(int Number, string Question, string Recommended, IReadOnlyList<string> Alternatives);
