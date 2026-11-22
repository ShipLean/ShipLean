using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// What an analysis run decided about a point it looked at. Wire values are camelCase.
[JsonConverter(typeof(JsonStringEnumConverter<RetroPointOutcome>))]
public enum RetroPointOutcome
{
    [JsonStringEnumMemberName("noise")]
    Noise,
    [JsonStringEnumMemberName("folded")]
    Folded,
    [JsonStringEnumMemberName("oneOff")]
    OneOff,
    [JsonStringEnumMemberName("clustered")]
    Clustered,
    [JsonStringEnumMemberName("unclustered")]
    Unclustered,
}
