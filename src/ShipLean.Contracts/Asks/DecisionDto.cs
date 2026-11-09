using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Asks;

// A decision an agent made on a work item, for its owner to confirm or change. Label is "D" plus the number.
// CurrentAnswer is the answer in force: the recommended one until it is changed. AnswerKind says which it is, and
// AlternativeIndex is the index into Alternatives when it is an alternative, otherwise null. The Answered* fields are null
// until it is confirmed, changed or withdrawn. FromDescription marks a decision backfilled from a D-table in the description.
// History is oldest first.
public sealed record DecisionDto(
    Guid Id,
    int Number,
    string Label,
    string Question,
    string Recommended,
    IReadOnlyList<string> Alternatives,
    string CurrentAnswer,
    AnswerKind AnswerKind,
    int? AlternativeIndex,
    DecisionState State,
    UserRef RaisedBy,
    string? RaisedAgentName,
    DateTimeOffset RaisedAt,
    string RaisedAtLabel,
    UserRef? AnsweredBy,
    string? AnsweredAgentName,
    DateTimeOffset? AnsweredAt,
    string? AnsweredAtLabel,
    bool FromDescription,
    IReadOnlyList<DecisionEventDto> History);
