using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.Umami;

// Builds the performance items of one Umami /api/batch body (the C8 format). Pure: no I/O.
// A key whose value is null is left out: Umami's schema is optional, not nullable, and refuses JSON null.
// There is no ip key: Umami must see the server's address, never the visitor's.
public static class UmamiWebVitalsPayload
{
    public static IReadOnlyDictionary<string, object?> PerformanceItem(WebVitalsSample sample, AnalyticsTarget target)
    {
        var payload = new Dictionary<string, object?>
        {
            ["website"] = target.SiteKey,
            ["hostname"] = target.InstanceHost,
            ["url"] = $"/web/{sample.Screen}",
            ["title"] = sample.Screen,
            ["language"] = string.Empty,
            ["screen"] = sample.ScreenSize,
            ["id"] = sample.DistinctId,
            ["userAgent"] = sample.UserAgent,
            ["lcp"] = sample.Lcp,
            ["inp"] = sample.Inp,
            ["cls"] = sample.Cls,
            ["fcp"] = sample.Fcp,
            ["ttfb"] = sample.Ttfb,
        };
        foreach (var key in payload.Where(pair => pair.Value is null).Select(pair => pair.Key).ToList())
        {
            payload.Remove(key);
        }
        return new Dictionary<string, object?> { ["type"] = "performance", ["payload"] = payload };
    }
}
