using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipLean.TestSupport.Inspection;

// Reads an attribute map whose values may be JSON numbers, booleans or arrays as well as strings, and turns each into the
// string K3 says a store returns: integers and doubles in invariant culture, booleans true|false, arrays as compact JSON.
public sealed class LenientAttributesConverter : JsonConverter<IReadOnlyDictionary<string, string>>
{
    public override IReadOnlyDictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => Text(p.Value), StringComparer.Ordinal);
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, text) in value.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            writer.WriteString(key, text);
        }
        writer.WriteEndObject();
    }

    private static string Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.TryGetInt64(out var whole)
            ? whole.ToString(CultureInfo.InvariantCulture)
            : value.GetDouble().ToString("R", CultureInfo.InvariantCulture),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText(),
    };
}
