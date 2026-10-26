using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipLean.Application.Features.Import.Jobs;

// How an import's persisted documents (progress, mapping rows, target) are written to jsonb: camelCase, enums as camelCase strings.
public static class ImportJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}
