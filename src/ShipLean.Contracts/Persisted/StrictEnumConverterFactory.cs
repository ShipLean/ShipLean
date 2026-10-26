using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Persisted;

// Enums travel as exactly their camelCase name (or their JsonStringEnumMemberName): no other casing, no integers, no comma lists.
public sealed class StrictEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(StrictEnumConverter<>).MakeGenericType(typeToConvert))!;
}
