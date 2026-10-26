using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Hosting.Proxy;

namespace ShipLean.AcceptanceTests.Harness;

// Base class for a scenario behind an identity proxy: the seed, the real proxy token reader and handler, and a
// browser whose every request carries the proxy's token header, as a proxy in front of the app would add it.
// No password sign-in happens.
public abstract class ProxyAcceptanceTest : SeededAcceptanceTest
{
    public const string ProxyEmail = "joel@shiplean.local";
    public const string ProxyName = "Joel Dickson";

    protected TestIdentityProvider Provider { get; private set; } = null!;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override IReadOnlyDictionary<string, string?> HostSettings
    {
        get
        {
            var settings = new Dictionary<string, string?>(base.HostSettings);
            foreach (var (key, value) in Provider.Settings())
            {
                settings[key] = value;
            }
            settings["SHIPLEAN_AUTH_PROXY_SIGN_OUT_URL"] = Provider.SignedOutUrl;
            return settings;
        }
    }

    protected override async Task BeforeHostStartAsync()
    {
        await base.BeforeHostStartAsync();
        Provider = await TestIdentityProvider.StartAsync();
    }

    [SetUp]
    public async Task SendProxyTokenAsync()
    {
        await Page.Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            [TestIdentityProvider.DefaultTokenHeader] = Provider.Mint(ProxyEmail, ProxyName),
        });
    }

    [OneTimeTearDown]
    public async Task StopProviderAsync()
    {
        if (Provider != null)
        {
            await Provider.DisposeAsync();
        }
    }
}
