namespace ShipLean.Contracts.Inbox;

// An open question on an Inbox row. Options are the quick answers it offers (at most InboxRules.MaxQuestionOptions), or
// empty.
public sealed record InboxQuestionDto(int Number, string Text, IReadOnlyList<string> Options);
