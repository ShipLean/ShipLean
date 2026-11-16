namespace ShipLean.Contracts.Views.InspectionMetrics;

// Escaping bugs over a period, a quarter or the range (SHIP-1398 QK3, design "Escaping bugs"). Escaping counts every Bug not marked
// found before release (design D2), in the sprint it was filed, or, once it has a Caused by link, the sprint its cause's lead time
// ended in (design D11). Weighted sums the project's severity weights over the escaping bugs (item D16; a bug with no severity
// weighs 0). FoundBeforeRelease counts the bugs left out of Escaping. MergedPrs counts pull requests linked to an item of the
// project that merged in the span (unlinked PRs are left out); Per100Prs is Escaping per 100 of them to one decimal, and both are
// null when no PR merged in the span. EscapingBySeverity splits Escaping by severity.
public sealed record InspectionQualityStatsDto(
    int Escaping,
    int Weighted,
    int FoundBeforeRelease,
    int? MergedPrs,
    double? Per100Prs,
    InspectionSeverityCountsDto EscapingBySeverity);
