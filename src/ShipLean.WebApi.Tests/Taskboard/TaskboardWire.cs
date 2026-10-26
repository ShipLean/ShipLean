using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Taskboard;

// Reads responses the way the API writes them: camelCase names, enums as camelCase strings.
public static class TaskboardWire
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<T>(text, Options)!;
    }

    public static async Task<T> GetAsync<T>(this ApiClient api, string path)
    {
        using var response = await api.GetAsync(path);
        return await response.ReadAsync<T>();
    }

    // The enum members go over the wire as camelCase strings, so a request body is serialised the same way.
    public static async Task<HttpResponseMessage> PostWireAsync(this ApiClient api, string path, object body)
        => await api.PostJsonAsync(path, JsonSerializer.SerializeToElement(body, Options));
}
