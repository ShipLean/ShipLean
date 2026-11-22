using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Contracts.Retros.Analysis;

// What record_retro_analysis asks for, as the tool has parsed it: the arguments only, no plan token. The digest the plan seals is over the tool's own arguments.
public sealed record RecordRetroAnalysisRequest(
    DecideScope Scope,
    bool ExcludeCovered,
    string EmbeddingModel,
    string? RewriteModel,
    IReadOnlyList<RetroAnalysisPoint> Points,
    IReadOnlyList<RetroAnalysisPattern> Patterns,
    IReadOnlyList<RetroAnalysisFix> Fixes,
    string ReportMarkdown);
