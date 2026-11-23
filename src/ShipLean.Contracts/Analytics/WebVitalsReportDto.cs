namespace ShipLean.Contracts.Analytics;

// One navigation's Core Web Vitals in POST /api/usage/web-vitals. Screen is a route id, never a path; a metric is null when the
// browser did not report it. ScreenSize is "<width>x<height>".
public sealed record WebVitalsReportDto(
    string Screen,
    WebVitalsNavigation Navigation,
    double? Lcp,
    double? Inp,
    double? Cls,
    double? Fcp,
    double? Ttfb,
    string? ScreenSize);
