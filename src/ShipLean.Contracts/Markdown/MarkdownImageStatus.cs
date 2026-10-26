using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Markdown;

// External: an absolute URL, never loaded. Attached: a bare name matching an image on the work item. Missing: anything else.
// The camelCase wire names are fixed per member, so they hold whatever serializer options a caller uses.
[JsonConverter(typeof(JsonStringEnumConverter<MarkdownImageStatus>))]
public enum MarkdownImageStatus
{
    [JsonStringEnumMemberName("attached")]
    Attached,
    [JsonStringEnumMemberName("external")]
    External,
    [JsonStringEnumMemberName("missing")]
    Missing,
}
