using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Asks;

// A question an agent or person asked on a work item. Label is "Q" plus the number. AgentName is the agent that asked it
// for AskedBy, otherwise null. Answer is null until it is answered; WithdrawnAt and WithdrawnAtLabel are null unless withdrawn.
public sealed record QuestionDto(
    Guid Id,
    int Number,
    string Label,
    string Text,
    QuestionState State,
    UserRef AskedBy,
    string? AgentName,
    DateTimeOffset AskedAt,
    string AskedAtLabel,
    QuestionAnswerDto? Answer,
    DateTimeOffset? WithdrawnAt,
    string? WithdrawnAtLabel);
