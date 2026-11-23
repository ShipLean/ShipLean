using System.Text.RegularExpressions;
using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.WebVitals;

// POST /api/usage/web-vitals: checks the whole batch first (count, screen ids, screen sizes), then queues one sample per report.
// A metric outside Umami's range is dropped from its report and counted on shiplean.web_vitals.discarded (WebVitalsMetrics); a report with no
// metric left is not queued. It reads no database and does not look at the switch: the dispatcher applies it.
[RegisterPerRequest]
public sealed partial class WebVitalsReports(ICurrentUser currentUser, IWebVitalsQueue queue, TimeProvider time, WebVitalsMetrics metrics)
{
    public const int MaxReports = 20;
    public const int MaxUserAgentLength = 512;
    public const double MaxTiming = 60_000;
    public const double MaxShift = 100;

    [GeneratedRegex(@"^\d{1,5}x\d{1,5}$", RegexOptions.CultureInvariant)]
    private static partial Regex ScreenSizePattern();

    public void Report(WebVitalsBatchDto batch, string? userAgent)
    {
        var reports = batch.Reports ?? [];
        if (reports.Count == 0 || reports.Count > MaxReports)
        {
            throw ApiException.Validation($"Send from 1 to {MaxReports} reports.", "reports");
        }
        foreach (var report in reports)
        {
            if (report is null)
            {
                throw ApiException.Validation("A report cannot be null.", "reports");
            }
            if (report.Screen is null || !ScreenIds.All.Contains(report.Screen))
            {
                throw ApiException.Validation("That is not a known screen.", "screen");
            }
            if (report.ScreenSize is not null && !ScreenSizePattern().IsMatch(report.ScreenSize))
            {
                throw ApiException.Validation("A screen size is <width>x<height>.", "screenSize");
            }
        }
        var agent = string.IsNullOrEmpty(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, MaxUserAgentLength)];
        var distinctId = UsageActor.ForUser(currentUser.Id).DistinctId;
        var now = time.GetUtcNow();
        foreach (var report in reports)
        {
            var lcp = InRange(report.Lcp, MaxTiming);
            var inp = InRange(report.Inp, MaxTiming);
            var cls = InRange(report.Cls, MaxShift);
            var fcp = InRange(report.Fcp, MaxTiming);
            var ttfb = InRange(report.Ttfb, MaxTiming);
            if (lcp is null && inp is null && cls is null && fcp is null && ttfb is null)
            {
                continue;
            }
            queue.Track(new WebVitalsSample(report.Screen, report.Navigation, lcp, inp, cls, fcp, ttfb, agent, report.ScreenSize, distinctId, now));
        }
    }

    private double? InRange(double? value, double max)
    {
        if (value is not { } v)
        {
            return null;
        }
        if (v >= 0 && v <= max)
        {
            return v;
        }
        metrics.Discarded.Add(1);
        return null;
    }
}
