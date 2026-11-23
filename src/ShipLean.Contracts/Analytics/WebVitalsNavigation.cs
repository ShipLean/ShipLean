using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Analytics;

// How the page got to the screen a reading is about: a full page load, or a route change inside the app. Wire values are explicit.
public enum WebVitalsNavigation
{
    [JsonStringEnumMemberName("hard")]
    Hard,

    [JsonStringEnumMemberName("soft")]
    Soft,
}
