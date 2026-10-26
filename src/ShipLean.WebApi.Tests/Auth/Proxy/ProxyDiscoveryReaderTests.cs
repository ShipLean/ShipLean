using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Token mode with no JWKS URL: the keys come from the issuer's discovery document (loopback http in the tests).
[TestFixture]
public class ProxyDiscoveryReaderTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => ProxyIdentityProbeController.AddTo;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => Provider!.DiscoverySettings();

    [Test]
    public async Task Read_WhenIssuerDiscovered_ShouldReturnIdentity()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader,
            Provider!.Mint("discovered@example.test", "Dis Covered", new TokenOptions { Issuer = Provider.DiscoveryIssuer }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBe(new ProxyIdentity("discovered@example.test", "Dis Covered"));
    }

    [Test]
    public async Task Read_WhenDiscoveredButIssuerWrong_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("default-issuer@example.test"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }
}
