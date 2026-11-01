using System.Globalization;
using System.Text.Json;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.PostHog;

// Builds the /batch/ request body (C6 PostHog). Person properties ride on $set only for the events the caller marks.
internal static class PostHogPayload
{
    public const string Lib = "shiplean";

    public static string Build(string apiKey, IReadOnlyList<AnalyticsEvent> batch, IReadOnlySet<int> eventsWithPerson)
    {
        var items = new List<Dictionary<string, object?>>(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            items.Add(BuildItem(batch[i], eventsWithPerson.Contains(i)));
        }
        var body = new Dictionary<string, object?>
        {
            ["api_key"] = apiKey,
            ["historical_migration"] = false,
            ["batch"] = items,
        };
        return JsonSerializer.Serialize(body);
    }

    private static Dictionary<string, object?> BuildItem(AnalyticsEvent evt, bool setPerson)
    {
        var properties = new Dictionary<string, object?>();
        foreach (var pair in evt.Properties)
        {
            properties[pair.Key] = pair.Value;
        }
        properties["$lib"] = Lib;
        if (evt.Properties.TryGetValue(AnalyticsProps.ProjectId, out var projectId))
        {
            properties["$groups"] = new Dictionary<string, object?> { ["project"] = projectId };
        }
        if (setPerson && evt.Person.Properties.Count > 0)
        {
            properties["$set"] = evt.Person.Properties;
        }
        return new Dictionary<string, object?>
        {
            ["event"] = evt.Name,
            ["distinct_id"] = evt.DistinctId,
            ["timestamp"] = evt.Timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            ["properties"] = properties,
        };
    }
}
