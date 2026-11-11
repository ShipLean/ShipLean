using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Asks;

// The decisions and questions on one work item, for its Decisions and questions section. Owner is who answers them:
// the assignee, or the creator when nobody is assigned (OwnerIsCreator); null only when neither has a user. The open
// counts cover this item's own asks. WaitingChildren is filled for a Feature only: its Stories and Bugs with open asks.
public sealed record ItemAsksDto(
    Guid WorkItemId,
    UserRef? Owner,
    bool OwnerIsCreator,
    bool OwnerIsMe,
    int OpenQuestions,
    int OpenDecisions,
    IReadOnlyList<QuestionDto> Questions,
    IReadOnlyList<DecisionDto> Decisions,
    IReadOnlyList<WaitingChildDto> WaitingChildren);
