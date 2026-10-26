using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// Reads responses the way the API writes them: camelCase names, enums as camelCase strings.
public static class SprintWire
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<T>(text, Options)!;
    }

    public static async Task<T> GetAsync<T>(this ApiClient api, string path)
    {
        using var response = await api.GetAsync(path);
        return await response.ReadAsync<T>();
    }
}
