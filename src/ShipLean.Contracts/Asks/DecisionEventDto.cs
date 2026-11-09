using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Asks;

// One line of a decision's history. From is the answer before a change or edit (null when the event has none). To is the
// answer in force after a raise, edit, confirm or change, and is null for withdrawn, asked and answered; ToKind is the kind of To. Note is the owner's reason for a change, if given. By is null for a user that no
// longer exists; AgentName is the agent that acted for By, otherwise null.
public sealed record DecisionEventDto(
    AskEventKind Kind,
    string? From,
    string? To,
    AnswerKind? ToKind,
    string? Note,
    UserRef? By,
    string? AgentName,
    DateTimeOffset At,
    string AtLabel,
    bool FromDescription);
