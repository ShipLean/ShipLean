using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// Whether a pattern is a recurring problem or something that went well and is worth keeping.
[JsonConverter(typeof(JsonStringEnumConverter<RetroPatternKind>))]
public enum RetroPatternKind
{
    [JsonStringEnumMemberName("problem")]
    Problem,
    [JsonStringEnumMemberName("wentWell")]
    WentWell,
}
