using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Inbox;

// One open ask on an Inbox row. Label is "Q1", "D3", "@1" and "@2" (mentions per item, oldest first), or "Status". From
// is who raised it, and AgentName the agent that did it for them, or null. Exactly one of Decision, Question, Mention and
// Status is set, matching Kind. A status ask's At is when the line was last verified, or the Feature's creation when it
// never was; its From and AgentName are who verified it (null when never).
public sealed record InboxAskDto(
    InboxAskKind Kind,
    string Label,
    DateTimeOffset At,
    string AtLabel,
    UserRef? From,
    string? AgentName,
    InboxDecisionDto? Decision,
    InboxQuestionDto? Question,
    InboxMentionDto? Mention,
    InboxStatusDto? Status);
