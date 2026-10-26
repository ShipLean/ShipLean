using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Attachments;

// What an upload would do: add a new file, replace the one of the same name, or be refused for size.
// The camelCase wire names are fixed per member, so they hold whatever serializer options a caller uses.
[JsonConverter(typeof(JsonStringEnumConverter<AttachmentOutcome>))]
public enum AttachmentOutcome
{
    [JsonStringEnumMemberName("add")]
    Add,
    [JsonStringEnumMemberName("replace")]
    Replace,
    [JsonStringEnumMemberName("tooLarge")]
    TooLarge,
    [JsonStringEnumMemberName("overTotal")]
    OverTotal,
}
