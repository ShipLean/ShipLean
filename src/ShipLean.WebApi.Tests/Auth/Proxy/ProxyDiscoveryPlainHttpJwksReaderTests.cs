using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Discovery whose document names a plain-http, non-loopback jwks_uri: it is never fetched, so no token passes.
[TestFixture]
public class ProxyDiscoveryPlainHttpJwksReaderTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => ProxyIdentityProbeController.AddTo;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => Provider!.DiscoverySettings();

    protected override async Task BeforeHostStartAsync()
    {
        await base.BeforeHostStartAsync();
        Provider!.DiscoveryJwksUriOverride = "http://keys.idp.test/jwks";
    }

    [Test]
    public async Task Read_WhenDiscoveredJwksUriIsPlainHttp_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader,
            Provider!.Mint("plainhttp@example.test", options: new TokenOptions { Issuer = Provider.DiscoveryIssuer }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }
}
