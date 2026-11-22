using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Contracts.Retros.Analysis;

// A preview (nothing written) or an apply of record_retro_analysis, minus the plan token the tool seals. AnalysisId is set on apply only.
// Created lists the keys of the action items an apply made.
public sealed record RetroAnalysisOutcome(
    RetroFunnel Funnel,
    IReadOnlyList<RetroRankedPattern> Patterns,
    IReadOnlyList<DecideCreateLine> WouldCreate,
    IReadOnlyList<DecideAddLine> WouldAdd,
    IReadOnlyList<DecideSkip> Skipped,
    IReadOnlyList<string> Warnings,
    Guid? AnalysisId,
    IReadOnlyList<string> Created);
