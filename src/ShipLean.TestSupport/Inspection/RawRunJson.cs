using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Inspection;

namespace ShipLean.TestSupport.Inspection;

// The JSON the fixtures use for a RawRun and an AssembledRun: the API's options (camelCase enums) plus lenient attribute values.
public static class RawRunJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static RawRun ParseRun(string json) => JsonSerializer.Deserialize<RawRun>(json, Options)!;

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.Converters.Add(new LenientAttributesConverter());
        return options;
    }
}
