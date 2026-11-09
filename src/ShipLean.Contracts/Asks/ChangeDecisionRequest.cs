namespace ShipLean.Contracts.Asks;

// Change a decision's answer: AlternativeIndex for an alternative, Text for the owner's own answer. Note is an optional reason.
public sealed record ChangeDecisionRequest(AnswerKind Kind, int? AlternativeIndex, string? Text, string? Note);
