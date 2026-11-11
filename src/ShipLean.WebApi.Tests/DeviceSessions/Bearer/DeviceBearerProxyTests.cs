using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.TestSupport;
using ShipLean.TestSupport.DeviceSessions;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.DeviceSessions.Bearer;

// Identity-proxy mode: a device bearer is never accepted. The proxy never reads identity on the upload and download
// URLs (ProxySignInPaths.Skipped), so a bearer must not sign in there either, or it would authenticate and touch the
// session on a path the proxy lets through.
[TestFixture]
public class DeviceBearerProxyTests : ProxyApiFixture
{
    private IDeviceSessions Store => Host.Services.GetRequiredService<IDeviceSessions>();

    [Test]
    public async Task Api_WhenProxyModeAndDeviceBearer_ShouldNotAuthenticate()
    {
        using var phone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel, now: TimeProvider.System.GetUtcNow().AddHours(-2));
        var before = await Store.FindBySecretAsync(phone.SignIn.Secret);
        using var plain = new HttpClient { BaseAddress = new Uri(Host.BaseUrl) };

        using var with = await phone.Client.GetAsync("/api/me");
        using var without = await plain.GetAsync("/api/me");

        (await Describe(with)).ShouldBe(await Describe(without));
        with.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Store.FindBySecretAsync(phone.SignIn.Secret)).ShouldBe(before);
    }

    [TestCase("PUT", "/api/uploads/not-a-live-token")]
    [TestCase("GET", "/api/downloads/not-a-live-token")]
    public async Task RefusedPath_WhenProxyModeAndDeviceBearerOnUploadsOrDownloads_ShouldNotSignIn(string method, string path)
    {
        using var phone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel, now: TimeProvider.System.GetUtcNow().AddHours(-2));
        var before = await Store.FindBySecretAsync(phone.SignIn.Secret);
        using var plain = new HttpClient { BaseAddress = new Uri(Host.BaseUrl) };

        using var with = await phone.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        using var without = await plain.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        (await Describe(with)).ShouldBe(await Describe(without));
        (await Store.FindBySecretAsync(phone.SignIn.Secret)).ShouldBe(before);
    }

    private static async Task<(int Status, string Code)> Describe(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        try
        {
            return ((int)response.StatusCode, JsonDocument.Parse(body).RootElement.TryGetProperty("code", out var c) ? c.GetString() ?? string.Empty : string.Empty);
        }
        catch (JsonException)
        {
            return ((int)response.StatusCode, string.Empty);
        }
    }
}
