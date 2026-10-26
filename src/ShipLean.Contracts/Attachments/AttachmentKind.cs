using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Attachments;

// What a file is for the UI: a Markdown file opens in the reader, an image previews, anything else downloads.
// The camelCase wire names are fixed per member, so they hold whatever serializer options a caller uses.
[JsonConverter(typeof(JsonStringEnumConverter<AttachmentKind>))]
public enum AttachmentKind
{
    [JsonStringEnumMemberName("markdown")]
    Markdown,
    [JsonStringEnumMemberName("image")]
    Image,
    [JsonStringEnumMemberName("file")]
    File,
}
