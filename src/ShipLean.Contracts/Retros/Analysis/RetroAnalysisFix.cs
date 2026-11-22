namespace ShipLean.Contracts.Retros.Analysis;

// Existing is the work item id of an open action item to add the pattern's points to; null lets the run create one.
public sealed record RetroAnalysisFix(string PatternRef, string Title, RetroFixType FixType, RetroEnforcement Enforcement, string? EnforcedBy, string Reason, Guid? Existing);
