using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Backups.Dtos;

// Writes an instant as ISO 8601 UTC with Z and second precision ("2026-10-24T14:00:00Z"), the pinned wire form of every
// backup timestamp. The default converter would write "+00:00". Reading takes only that form, so no zone gets in.
public sealed class UtcSecondsJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (text is null
            || !DateTimeOffset.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
        {
            throw new JsonException($"Expected a UTC timestamp written {Format}.");
        }
        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
}
