using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Analytics;

// Which analytics service an instance sends usage events to. Wire values are "none", "umami" and "posthog": the default camelCase
// would write PostHog as "postHog", so the member names are explicit.
public enum AnalyticsProvider
{
    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("umami")]
    Umami,

    [JsonStringEnumMemberName("posthog")]
    PostHog,
}
