using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// How much of a proposed fix what already exists enforces. Fully and discarded never create or extend an action item.
[JsonConverter(typeof(JsonStringEnumConverter<RetroEnforcement>))]
public enum RetroEnforcement
{
    [JsonStringEnumMemberName("notCaught")]
    NotCaught,
    [JsonStringEnumMemberName("partly")]
    Partly,
    [JsonStringEnumMemberName("fully")]
    Fully,
    [JsonStringEnumMemberName("discarded")]
    Discarded,
}
