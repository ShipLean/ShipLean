namespace ShipLean.Contracts.Asks;

// How a backfilled decision was already answered: who, on which day, and with which answer (AlternativeIndex for an
// alternative, Text for the owner's own answer).
public sealed record SettledAnswer(Guid ByUserId, DateOnly On, AnswerKind Kind, int? AlternativeIndex, string? Text);
