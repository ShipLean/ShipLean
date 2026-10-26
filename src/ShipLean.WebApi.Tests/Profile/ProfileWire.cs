using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Profile;

// Reads MeDto the way the wire writes it (camelCase enum strings).
internal static class ProfileWire
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static async Task<MeDto> ReadMeAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<MeDto>(Options))!;

    public static async Task<MeDto> GetMeAsync(ApiClient api, TestUser user)
    {
        using var response = await api.ActingAs(user).GetAsync("/api/me");
        return await ReadMeAsync(response);
    }
}
