namespace ShipLean.Contracts.Inbox;

// What an Inbox ask is: a decision to confirm, a question to answer, an @mention to reply to or mark seen, or a status
// line to check. On the wire as camelCase strings.
public enum InboxAskKind
{
    Decision,
    Question,
    Mention,
    Status,
}
