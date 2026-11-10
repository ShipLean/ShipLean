namespace ShipLean.Contracts.Asks;

// What the owner does with decision Number, in-process (IDecisionAnswers). Kind, AlternativeIndex and Text are for Change
// only; Note is the optional reason for a change or withdrawal.
public sealed record DecisionAnswer(int Number, DecisionAnswerAction Action, AnswerKind? Kind, int? AlternativeIndex, string? Text, string? Note);
