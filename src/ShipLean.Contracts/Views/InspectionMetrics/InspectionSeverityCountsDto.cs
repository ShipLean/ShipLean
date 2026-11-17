namespace ShipLean.Contracts.Views.InspectionMetrics;

// Escaping bugs split by severity (SHIP-1398 QK3), for the Quality view's and the Overview's stacked bars: Sev1 (critical) to Sev4
// (low), and Unset for a bug with no severity. Escaping bugs only; the five always sum to InspectionQualityStatsDto.Escaping.
public sealed record InspectionSeverityCountsDto(int Sev1, int Sev2, int Sev3, int Sev4, int Unset);
