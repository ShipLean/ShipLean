using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Persisted;

internal sealed class StrictEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    private readonly Dictionary<string, T> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<T, string> _byValue = [];

    public StrictEnumConverter()
    {
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (T)field.GetValue(null)!;
            var name = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
                       ?? JsonNamingPolicy.CamelCase.ConvertName(field.Name);
            _byName.Add(name, value);
            _byValue.Add(value, name);
        }
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } text && _byName.TryGetValue(text, out var value))
        {
            return value;
        }
        throw new JsonException($"Not a valid {typeof(T).Name}: {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!_byValue.TryGetValue(value, out var name))
        {
            throw new JsonException($"Not a defined {typeof(T).Name}: {value}.");
        }
        writer.WriteStringValue(name);
    }
}
