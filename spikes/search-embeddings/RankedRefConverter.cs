using System.Text.Json;
using System.Text.Json.Serialization;

namespace SearchEmbeddings;

public sealed class RankedRefConverter : JsonConverter<RankedRef>
{
    public override RankedRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("A ranked entry is [ref, score].");
        }
        reader.Read();
        var id = reader.GetString() ?? throw new JsonException("Missing ref.");
        reader.Read();
        double? score = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
        reader.Read();
        return new RankedRef(id, score);
    }

    public override void Write(Utf8JsonWriter writer, RankedRef value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.Ref);
        if (value.Score is { } score)
        {
            writer.WriteNumberValue(score);
        }
        else
        {
            writer.WriteNullValue();
        }
        writer.WriteEndArray();
    }
}
