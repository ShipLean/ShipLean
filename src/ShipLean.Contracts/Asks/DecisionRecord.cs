namespace ShipLean.Contracts.Asks;

// One decision to record, in-process (IDecisionRecorder). Number null takes the next number; a free number takes exactly
// that; an open number edits it. Settled is set only by the D-table backfill: the decision is created already answered.
public sealed record DecisionRecord(int? Number, string Question, string Recommended, IReadOnlyList<string> Alternatives, SettledAnswer? Settled);
