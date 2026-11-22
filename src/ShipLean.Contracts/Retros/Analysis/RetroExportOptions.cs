namespace ShipLean.Contracts.Retros.Analysis;

// PatternsFromLatest asks for the newest live analysis's patterns; PatternsFromAnalysis for one analysis's. Both unset: none. Limit is 1 to 100.
public sealed record RetroExportOptions(bool IncludeWentWell, bool ExcludeCovered, bool PatternsFromLatest, Guid? PatternsFromAnalysis, int Limit, RetroExportCursor? After);
