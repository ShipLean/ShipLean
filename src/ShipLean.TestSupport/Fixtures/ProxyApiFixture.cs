using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.TestSupport.Fixtures;

// Base class for proxy sign-in API tests: an ApiFixture with no test sign-in, so only the real schemes sign anyone in,
// and the host started in proxy token mode. By default the reader is FakeProxyIdentityReader (identity from two test
// headers, ProxyClient().WithIdentity). Set UseFakeReader false to run the real reader, and UseIdentityProvider true to
// start a TestIdentityProvider before the host (Provider) whose settings the host then gets.
public abstract class ProxyApiFixture : ApiFixture
{
    // Literal token settings for a host whose reader is the fake: the JWKS URL is never fetched.
    public static IReadOnlyDictionary<string, string?> LiteralTokenSettings { get; } =
        new Dictionary<string, string?>
        {
            ["SHIPLEAN_AUTH_PROXY_TOKEN_HEADER"] = TestIdentityProvider.DefaultTokenHeader,
            ["SHIPLEAN_AUTH_PROXY_TOKEN_ISSUER"] = TestIdentityProvider.Issuer,
            ["SHIPLEAN_AUTH_PROXY_TOKEN_JWKS_URL"] = "http://127.0.0.1:9/jwks",
        };

    // Started in BeforeHostStartAsync when UseIdentityProvider; disposed in AfterHostStopAsync, after the host.
    protected TestIdentityProvider? Provider { get; private set; }

    protected virtual bool UseFakeReader => true;

    protected virtual bool UseIdentityProvider => false;

    // The proxy settings the host starts with: the provider's token settings, or else LiteralTokenSettings. Override
    // for plain-header mode (TestIdentityProvider.HeaderSettings()), discovery or an audience.
    protected virtual IReadOnlyDictionary<string, string?> ProxySettings => Provider?.Settings() ?? LiteralTokenSettings;

    // A subclass's own doubles, applied after the fake-reader swap.
    protected virtual Action<IServiceCollection>? ConfigureProxyTestServices => null;

    protected sealed override bool UseTestSignIn => false;

    // Services called straight from Host.Services act as nobody, as a request with no identity would.
    protected override TestUser SignedInAs => TestUser.Anonymous;

    // ProxySettings; to add other settings, override this and merge base.HostSettings.
    protected override IReadOnlyDictionary<string, string?> HostSettings => ProxySettings;

    // Sealed: put doubles in ConfigureProxyTestServices.
    protected sealed override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        if (UseFakeReader)
        {
            services.Replace(ServiceDescriptor.Singleton<IProxyIdentityReader, FakeProxyIdentityReader>());
        }
        ConfigureProxyTestServices?.Invoke(services);
    };

    // A subclass that overrides this calls base first.
    protected override async Task BeforeHostStartAsync()
    {
        await base.BeforeHostStartAsync();
        if (UseIdentityProvider)
        {
            Provider = await TestIdentityProvider.StartAsync();
        }
    }

    // A new browser with its own cookie jar; pass a non-loopback base address (LocalAddresses.OtherThanLoopback, with
    // ListenOnAllInterfaces) for an untrusted peer. Dispose it.
    protected ProxyApiClient ProxyClient(Uri? baseAddress = null) => new(baseAddress ?? new Uri(Host.BaseUrl));

    // After the host, so nothing in the host fetches the JWKS from a provider that has gone. A subclass that overrides
    // this calls base.
    protected override async Task AfterHostStopAsync()
    {
        if (Provider != null)
        {
            await Provider.DisposeAsync();
        }
        await base.AfterHostStopAsync();
    }
}
