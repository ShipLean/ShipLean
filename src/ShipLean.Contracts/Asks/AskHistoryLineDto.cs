using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Asks;

// One ask line in a work item's History. Badge names the ask ("D3", "Q1"), or a run of decisions raised together by the
// same person and agent at the same moment ("D1–D5", with an en dash). From, To and Note are as on DecisionEventDto (To is the answer in force after a raise, edit, confirm or change; null for withdrawn, asked and answered).
public sealed record AskHistoryLineDto(
    AskEventKind Kind,
    string Badge,
    string? From,
    string? To,
    string? Note,
    UserRef? By,
    string? AgentName,
    DateTimeOffset At,
    string AtLabel);
