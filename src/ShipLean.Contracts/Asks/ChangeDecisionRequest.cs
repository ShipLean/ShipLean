namespace ShipLean.Contracts.Asks;

// Change a decision's answer: AlternativeIndex for an alternative, Text for the owner's own answer. Note is an optional reason. Kind is nullable on the wire so a missing one is a validation error, not a default.
public sealed record ChangeDecisionRequest(AnswerKind? Kind, int? AlternativeIndex, string? Text, string? Note);
