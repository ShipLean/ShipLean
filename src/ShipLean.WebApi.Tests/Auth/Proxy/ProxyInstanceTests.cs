using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ShipLean.Application.Identity;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// GET /api/auth/instance tells the SPA which sign-in is active (K7). Each test starts its own host (no test sign-in) on the
// class's one clone, because the mode is fixed at startup.
[TestFixture]
public class ProxyInstanceTests
{
    private ClonedDatabase _database = null!;

    [OneTimeSetUp]
    public async Task CloneAsync() => _database = await ShipLeanDatabase.CloneSeedAsync();

    [OneTimeTearDown]
    public async Task DropAsync() => await _database.DisposeAsync();

    [Test]
    public async Task Instance_WhenNoProxySettings_ShouldReportPassword()
    {
        var instance = await ReadInstanceAsync(new Dictionary<string, string?>
        {
            // Ignored without an identity setting.
            [ProxySignInSettings.SignOutUrlKey] = "/oauth2/sign_out",
        });

        instance["name"]!.GetValue<string>().ShouldBe("Larkspur Engineering");
        instance["signIn"]!.GetValue<string>().ShouldBe("password");
        instance["signOutUrl"].ShouldBeNull();
    }

    [Test]
    public async Task Instance_WhenProxyTokenConfigured_ShouldReportProxyAndSignOutUrl()
    {
        var instance = await ReadInstanceAsync(new Dictionary<string, string?>
        {
            [ProxySignInSettings.TokenHeaderKey] = "X-Test-Proxy-Token",
            [ProxySignInSettings.IssuerKey] = "https://idp.shiplean.test",
            [ProxySignInSettings.JwksUrlKey] = "http://127.0.0.1:9/jwks",
            [ProxySignInSettings.SignOutUrlKey] = "/oauth2/sign_out",
        });

        instance["name"]!.GetValue<string>().ShouldBe("Larkspur Engineering");
        instance["signIn"]!.GetValue<string>().ShouldBe("proxy");
        instance["signOutUrl"]!.GetValue<string>().ShouldBe("/oauth2/sign_out");
    }

    private async Task<JsonNode> ReadInstanceAsync(IReadOnlyDictionary<string, string?> settings)
    {
        await using var host = ShipLeanHost.Start(_database.ConnectionString, user: null, settings: settings);
        using var client = host.CreateClient();
        return (await client.GetFromJsonAsync<JsonNode>("/api/auth/instance"))!;
    }
}
