using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ShipLean.Application.Identity;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// A bad SHIPLEAN_AUTH_PROXY_* setting fails host start, and the failure names the setting. Each test starts its own host
// (no test sign-in) on the class's one clone.
[TestFixture]
public class ProxySettingsStartupTests
{
    private const string Issuer = "https://idp.shiplean.test";

    private ClonedDatabase _database = null!;

    [OneTimeSetUp]
    public async Task CloneAsync() => _database = await ShipLeanDatabase.CloneSeedAsync();

    [OneTimeTearDown]
    public async Task DropAsync() => await _database.DisposeAsync();

    [Test]
    public void Startup_WhenTokenHeaderHasNoIssuer_ShouldFailNamingIssuer()
    {
        var failure = StartFailure(new() { [ProxySignInSettings.TokenHeaderKey] = "X-Test-Proxy-Token" });

        failure.ShouldContain(ProxySignInSettings.IssuerKey);
    }

    [Test]
    public void Startup_WhenBothIdentitySettingsSet_ShouldFailNamingBoth()
    {
        var failure = StartFailure(new()
        {
            [ProxySignInSettings.TokenHeaderKey] = "X-Test-Proxy-Token",
            [ProxySignInSettings.IssuerKey] = Issuer,
            [ProxySignInSettings.EmailHeaderKey] = "Remote-Email",
        });

        failure.ShouldContain(ProxySignInSettings.TokenHeaderKey);
        failure.ShouldContain(ProxySignInSettings.EmailHeaderKey);
    }

    [TestCase("http://idp.shiplean.test")]
    [TestCase("http://localhost.evil.com")]
    [TestCase("http://127.0.0.1@evil.com")]
    [TestCase("idp.shiplean.test")]
    public void Startup_WhenIssuerIsPlainHttp_ShouldFailNamingIssuer(string issuer)
    {
        var failure = StartFailure(new()
        {
            [ProxySignInSettings.TokenHeaderKey] = "X-Test-Proxy-Token",
            [ProxySignInSettings.IssuerKey] = issuer,
        });

        failure.ShouldContain(ProxySignInSettings.IssuerKey);
    }

    [TestCase("http://idp.shiplean.test/jwks")]
    [TestCase("http://localhost.evil.com/jwks")]
    public void Startup_WhenJwksUrlIsNonLoopbackHttp_ShouldFailNamingJwksUrl(string jwksUrl)
    {
        var failure = StartFailure(new()
        {
            [ProxySignInSettings.TokenHeaderKey] = "X-Test-Proxy-Token",
            [ProxySignInSettings.IssuerKey] = Issuer,
            [ProxySignInSettings.JwksUrlKey] = jwksUrl,
        });

        failure.ShouldContain(ProxySignInSettings.JwksUrlKey);
    }

    [TestCase(ProxySignInSettings.IssuerKey)]
    [TestCase(ProxySignInSettings.JwksUrlKey)]
    public void Startup_WhenKeyUrlHasCredentials_ShouldFailWithoutEchoingThem(string key)
    {
        var settings = new Dictionary<string, string?>
        {
            [ProxySignInSettings.TokenHeaderKey] = "X-Test-Proxy-Token",
            [ProxySignInSettings.IssuerKey] = Issuer,
        };
        settings[key] = "https://idp-user:idp-secret@idp.shiplean.test/jwks";

        var failure = StartFailure(settings);

        failure.ShouldContain(key);
        failure.ShouldNotContain("idp-secret");
    }

    [Test]
    public async Task Startup_WhenJwksUrlIsLoopbackHttp_ShouldStart()
    {
        await using var host = ShipLeanHost.Start(_database.ConnectionString, user: null, settings: new Dictionary<string, string?>
        {
            [ProxySignInSettings.TokenHeaderKey] = "X-Test-Proxy-Token",
            [ProxySignInSettings.IssuerKey] = Issuer,
            [ProxySignInSettings.JwksUrlKey] = "http://127.0.0.1:9/jwks",
        });
        using var client = host.CreateClient();

        var instance = await client.GetFromJsonAsync<JsonNode>("/api/auth/instance");

        instance!["signIn"]!.GetValue<string>().ShouldBe("proxy");
    }

    [TestCase("oauth2/sign_out")]
    [TestCase("//evil.com/sign_out")]
    [TestCase("/\\evil.com/sign_out")]
    [TestCase("/oauth2\\sign_out")]
    [TestCase("https://user:secret@idp.shiplean.test/sign_out")]
    public void Startup_WhenSignOutUrlIsRelativeWithoutSlash_ShouldFailNamingSignOutUrl(string signOutUrl)
    {
        var failure = StartFailure(new()
        {
            [ProxySignInSettings.EmailHeaderKey] = "Remote-Email",
            [ProxySignInSettings.SignOutUrlKey] = signOutUrl,
        });

        failure.ShouldContain(ProxySignInSettings.SignOutUrlKey);
        failure.ShouldNotContain("secret");
    }

    // Every message in the failure's chain, so the assertion doesn't depend on how the host wraps a hosted service's throw.
    private string StartFailure(Dictionary<string, string?> settings)
    {
        var exception = Should.Throw<Exception>(() => ShipLeanHost.Start(_database.ConnectionString, user: null, settings: settings));
        var messages = new List<string>();
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            messages.Add(e.Message);
        }
        return string.Join(" | ", messages);
    }
}
