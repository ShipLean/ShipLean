using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Persisted;

// The one set of options for every reader and writer of a persisted JSON column and for the pinned shapes.
// Nothing is omitted when writing: absent values are null and lists are never null.
public static class PersistedJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        options.Converters.Add(new StrictEnumConverterFactory());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
