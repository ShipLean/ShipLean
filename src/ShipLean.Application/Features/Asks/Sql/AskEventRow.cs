namespace ShipLean.Application.Features.Asks.Sql;

// One ask_event row. Exactly one of DecisionId and QuestionId is set (the table checks it). Kind is raised | edited |
// confirmed | changed | withdrawn | asked | answered; ToKind is recommended | alternative | own. At is UTC.
public sealed record AskEventRow(
    Guid Id,
    Guid WorkItemId,
    Guid? DecisionId,
    Guid? QuestionId,
    string Kind,
    string? FromAnswer,
    string? ToAnswer,
    string? ToKind,
    string? Note,
    Guid? ByUserId,
    string? AgentName,
    DateTimeOffset At,
    bool FromDescription);
