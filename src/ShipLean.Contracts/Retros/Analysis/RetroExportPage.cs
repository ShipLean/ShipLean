namespace ShipLean.Contracts.Retros.Analysis;

// One page of an export. TotalPoints counts the scope across all pages; NextCursor is null on the last page. OpenActions and PreviousPatterns are filled on the first page only.
public sealed record RetroExportPage(
    RetroScopeView Scope,
    int TotalPoints,
    RetroExportCursor? NextCursor,
    IReadOnlyList<RetroExportPoint> Points,
    IReadOnlyDictionary<Guid, RetroExportRetro> Retros,
    IReadOnlyList<RetroExportOpenAction> OpenActions,
    IReadOnlyList<RetroExportPattern> PreviousPatterns);
