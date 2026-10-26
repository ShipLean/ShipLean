using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// Reads a response the way the SPA does: web defaults, enums as camelCase strings (ApiClient.GetJsonAsync reads enums as numbers).
internal static class AttachmentWire
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static async Task<T> GetAsync<T>(ApiClient api, string url)
    {
        using var response = await api.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Options))!;
    }
}
