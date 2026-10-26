using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// In proxy mode the guard covers every /api write, with or without ShipLean's cookie (K10).
[TestFixture]
public class ProxyWriteGuardTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => ProxyApiFixture.LiteralTokenSettings;

    [Test]
    public async Task WriteGuard_WhenProxyModeFormPostWithoutCookie_ShouldBeForbidden()
    {
        using var client = new HttpClient { BaseAddress = new Uri(Host.BaseUrl) };

        using var response = await client.PostAsync("/api/auth/logout", new FormUrlEncodedContent([new("a", "b")]));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [Test]
    public async Task WriteGuard_WhenProxyModeJsonWithoutCookie_ShouldPass()
    {
        using var client = new HttpClient { BaseAddress = new Uri(Host.BaseUrl) };

        using var response = await client.PostAsync("/api/auth/logout", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task WriteGuard_WhenProxyModeClientHeaderWithoutCookie_ShouldPass()
    {
        using var client = new HttpClient { BaseAddress = new Uri(Host.BaseUrl) };
        client.DefaultRequestHeaders.Add("X-ShipLean-Client", "tab-1");

        using var response = await client.PostAsync("/api/auth/logout", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
