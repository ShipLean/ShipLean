using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Contracts;
using ShipLean.Contracts.Auth;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// A browser stand-in for the sign-in tests: its own cookie jar, no redirects followed, the client header the SPA sends
// (CrossSiteWriteGuard) unless a test leaves it out, and sign-in through the real /api/auth/login, so its requests are
// authenticated by the cookie and not by the test scheme.
public static class AuthClient
{
    // The API's serializer: web defaults, enums as camelCase strings.
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) } };

    public static HttpClient Create(ShipLeanHost host, bool sendClientHeader = true)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(host.BaseUrl) };
        if (sendClientHeader)
        {
            client.DefaultRequestHeaders.Add(ClientHeader.Name, "auth-tests");
        }
        return client;
    }

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password)
        => client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

    public static async Task<HttpClient> SignedInAsync(ShipLeanHost host, string email, string password)
    {
        var client = Create(host);
        using var response = await client.LoginAsync(email, password);
        response.EnsureSuccessStatusCode();
        return client;
    }
}
