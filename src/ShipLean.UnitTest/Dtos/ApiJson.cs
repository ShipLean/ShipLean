using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipLean.UnitTest.Dtos;

// The API's JSON options for the DTO fixture tests: web defaults, enums as camelCase strings. One place, so a change to
// the API's serializer is a change to one factory.
internal static class ApiJson
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
