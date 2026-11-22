using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// What an analysis or an export covers: a Feature, a sprint or a range of days.
[JsonConverter(typeof(JsonStringEnumConverter<RetroScopeKind>))]
public enum RetroScopeKind
{
    [JsonStringEnumMemberName("feature")]
    Feature,
    [JsonStringEnumMemberName("sprint")]
    Sprint,
    [JsonStringEnumMemberName("range")]
    Range,
}
