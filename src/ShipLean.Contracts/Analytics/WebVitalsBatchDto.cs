namespace ShipLean.Contracts.Analytics;

// POST /api/usage/web-vitals: from 1 to 20 reports.
public sealed record WebVitalsBatchDto(IReadOnlyList<WebVitalsReportDto> Reports);
