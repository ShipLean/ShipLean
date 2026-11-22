namespace ShipLean.Contracts.Retros.Analysis;

// One stored analysis. Patterns holds the problem patterns ranked, WentWell the went-well ones; FixesByTier is keyed by fix type (fix, structure, analyzer, test, skill, guide, product, other).
// Discarded holds the fixes the agent threw out.
public sealed record RetroAnalysisView(
    Guid Id,
    RetroScopeView Scope,
    DateTimeOffset CreatedAt,
    string? AgentName,
    string EmbeddingModel,
    string? RewriteModel,
    RetroFunnel Funnel,
    IReadOnlyList<RetroViewPattern> Patterns,
    IReadOnlyList<RetroViewPattern> WentWell,
    IReadOnlyDictionary<string, IReadOnlyList<RetroViewFix>> FixesByTier,
    IReadOnlyList<RetroViewFix> Discarded,
    string ReportMarkdown);
