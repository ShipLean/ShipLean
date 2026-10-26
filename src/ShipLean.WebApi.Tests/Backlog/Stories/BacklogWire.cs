using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// Reads the view the way the API writes it: camelCase names, enums as camelCase strings.
public static class BacklogWire
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static async Task<BacklogViewDto> GetBacklogAsync(this ApiClient api, string path)
    {
        using var response = await api.GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<BacklogViewDto>(text, Options)!;
    }
}
