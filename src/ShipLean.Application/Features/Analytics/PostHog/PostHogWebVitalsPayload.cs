using System.Globalization;
using System.Text.Json;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.PostHog;

// Builds the /batch/ request body for $web_vitals events (C9). PostHog has no property for TTFB, so it is never sent.
internal static class PostHogWebVitalsPayload
{
    // The envelope for the samples that carry at least one metric PostHog knows, or null when none does.
    public static string? Build(string apiKey, IReadOnlyList<WebVitalsSample> batch, string instanceHost)
    {
        var items = batch.Where(HasMetric).Select(sample => BuildItem(sample, instanceHost)).ToList();
        if (items.Count == 0)
        {
            return null;
        }
        var body = new Dictionary<string, object?>
        {
            ["api_key"] = apiKey,
            ["historical_migration"] = false,
            ["batch"] = items,
        };
        return JsonSerializer.Serialize(body);
    }

    private static bool HasMetric(WebVitalsSample sample)
    {
        return sample.Lcp is not null || sample.Inp is not null || sample.Cls is not null || sample.Fcp is not null;
    }

    private static Dictionary<string, object?> BuildItem(WebVitalsSample sample, string instanceHost)
    {
        var properties = new Dictionary<string, object?>();
        Add(properties, "$web_vitals_LCP_value", sample.Lcp);
        Add(properties, "$web_vitals_INP_value", sample.Inp);
        Add(properties, "$web_vitals_CLS_value", sample.Cls);
        Add(properties, "$web_vitals_FCP_value", sample.Fcp);
        var pathname = "/web/" + sample.Screen;
        properties["$pathname"] = pathname;
        if (!string.IsNullOrEmpty(instanceHost))
        {
            properties["$host"] = instanceHost;
            properties["$current_url"] = "https://" + instanceHost + pathname;
        }
        if (!string.IsNullOrEmpty(sample.UserAgent))
        {
            var summary = UserAgentSummary.Parse(sample.UserAgent);
            properties["$raw_user_agent"] = sample.UserAgent;
            if (summary.Browser is not null)
            {
                properties["$browser"] = summary.Browser;
            }
            if (summary.Os is not null)
            {
                properties["$os"] = summary.Os;
            }
            properties["$device_type"] = summary.DeviceType;
        }
        properties["$lib"] = PostHogPayload.Lib;
        properties["$process_person_profile"] = false;
        properties["screen"] = sample.Screen;
        properties["navigation"] = sample.Navigation == WebVitalsNavigation.Soft ? "soft" : "hard";
        return new Dictionary<string, object?>
        {
            ["event"] = "$web_vitals",
            ["distinct_id"] = sample.DistinctId,
            ["timestamp"] = sample.OccurredAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            ["properties"] = properties,
        };
    }

    private static void Add(Dictionary<string, object?> properties, string key, double? value)
    {
        if (value is not null)
        {
            properties[key] = value.Value;
        }
    }
}
