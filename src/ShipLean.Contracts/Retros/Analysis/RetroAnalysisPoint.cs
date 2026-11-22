namespace ShipLean.Contracts.Retros.Analysis;

// A point the agent looked at. FoldedInto is set for folded; Generic (the rewrite) for clustered, unclustered and oneOff.
public sealed record RetroAnalysisPoint(Guid Id, RetroPointOutcome Outcome, Guid? FoldedInto, string? Generic);
