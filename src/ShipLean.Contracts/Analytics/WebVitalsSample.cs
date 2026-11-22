namespace ShipLean.Contracts.Analytics;

// One accepted reading on its way to a provider: out-of-range metrics are already null, the user agent is cut to 512 characters
// and DistinctId is the UsageActor convention ("user:<guid>").
public sealed record WebVitalsSample(
    string Screen,
    WebVitalsNavigation Navigation,
    double? Lcp,
    double? Inp,
    double? Cls,
    double? Fcp,
    double? Ttfb,
    string? UserAgent,
    string? ScreenSize,
    string DistinctId,
    DateTimeOffset OccurredAt);
