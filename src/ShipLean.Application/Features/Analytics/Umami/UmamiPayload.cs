using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Analytics.Umami;

// Builds the items of one Umami /api/batch body (the C6 Umami format). Pure: no I/O.
public static class UmamiPayload
{
    public const int MaxProperties = 50;
    public const int MaxStringLength = 500;

    // Standard properties are kept first when a payload has to be trimmed to MaxProperties.
    public static readonly IReadOnlySet<string> StandardKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        AnalyticsProps.Surface,
        AnalyticsProps.ActorKind,
        AnalyticsProps.UserId,
        AnalyticsProps.AgentId,
        AnalyticsProps.AgentName,
        AnalyticsProps.AgentHost,
        AnalyticsProps.AgentVerified,
        AnalyticsProps.ProjectId,
        AnalyticsProps.ProjectKey,
        AnalyticsProps.ProjectCount,
        AnalyticsProps.Outcome,
        AnalyticsProps.ErrorCode,
        AnalyticsProps.DurationMs,
        AnalyticsProps.Source,
        AnalyticsProps.ShipLeanVersion,
        AnalyticsProps.InstanceHost,
    };

    public static IReadOnlyDictionary<string, object?> EventItem(AnalyticsEvent analyticsEvent, AnalyticsTarget target)
    {
        var surface = analyticsEvent.Properties.TryGetValue(AnalyticsProps.Surface, out var value) ? value?.ToString() : null;
        var url = $"/p/{(string.IsNullOrEmpty(analyticsEvent.ProjectKey) ? "_" : analyticsEvent.ProjectKey)}/{(string.IsNullOrEmpty(surface) ? "_" : surface)}/{analyticsEvent.Name.Replace('.', '/')}";
        return new Dictionary<string, object?>
        {
            ["type"] = "event",
            ["payload"] = new Dictionary<string, object?>
            {
                ["website"] = target.SiteKey,
                ["hostname"] = target.InstanceHost,
                ["url"] = url,
                ["title"] = analyticsEvent.Name,
                ["name"] = analyticsEvent.Name,
                ["id"] = analyticsEvent.DistinctId,
                ["tag"] = analyticsEvent.ActorKind,
                ["language"] = string.Empty,
                ["screen"] = string.Empty,
                ["referrer"] = string.Empty,
                ["data"] = Data(analyticsEvent.Properties),
            },
        };
    }

    public static IReadOnlyDictionary<string, object?> PageViewItem(AnalyticsEvent analyticsEvent, AnalyticsTarget target)
    {
        var project = string.IsNullOrEmpty(analyticsEvent.ProjectKey) ? "_" : analyticsEvent.ProjectKey;
        var screen = analyticsEvent.Properties.TryGetValue(UsageProps.Screen, out var value) ? value.ToString() : null;
        var payload = new Dictionary<string, object?>
        {
            ["website"] = target.SiteKey,
            ["hostname"] = target.InstanceHost,
            ["url"] = $"/p/{project}/{Uri.EscapeDataString(string.IsNullOrEmpty(screen) ? "_" : screen)}",
            ["title"] = screen,
            ["id"] = analyticsEvent.DistinctId,
            ["tag"] = analyticsEvent.ActorKind,
            ["language"] = string.Empty,
            ["screen"] = string.Empty,
            ["referrer"] = string.Empty,
        };
        return new Dictionary<string, object?> { ["type"] = "event", ["payload"] = payload };
    }

    public static IReadOnlyDictionary<string, object?> IdentifyItem(AnalyticsPerson person, AnalyticsTarget target)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "identify",
            ["payload"] = new Dictionary<string, object?>
            {
                ["website"] = target.SiteKey,
                ["hostname"] = target.InstanceHost,
                ["id"] = person.DistinctId,
                ["data"] = Data(person.Properties),
            },
        };
    }

    // At most MaxProperties entries, standard keys first; strings cut to MaxStringLength.
    public static IReadOnlyDictionary<string, object?> Data(IReadOnlyDictionary<string, object> properties)
    {
        var data = new Dictionary<string, object?>();
        foreach (var pair in properties.Where(p => StandardKeys.Contains(p.Key)).Concat(properties.Where(p => !StandardKeys.Contains(p.Key))).Take(MaxProperties))
        {
            data[pair.Key] = Value(pair.Value);
        }
        return data;
    }

    private static object? Value(object value)
    {
        return value switch
        {
            string text => Cut(text),
            bool or long or double => value,
            int number => (long)number,
            _ => Cut(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
        };
    }

    private static string Cut(string text)
    {
        return text.Length <= MaxStringLength ? text : text[..MaxStringLength];
    }
}
