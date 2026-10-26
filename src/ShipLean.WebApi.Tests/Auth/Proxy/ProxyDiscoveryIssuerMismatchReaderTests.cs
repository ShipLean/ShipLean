using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Discovery whose document names another issuer: its keys are not trusted, even for a token the configured issuer
// signed with them.
[TestFixture]
public class ProxyDiscoveryIssuerMismatchReaderTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => ProxyIdentityProbeController.AddTo;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => Provider!.DiscoverySettings();

    protected override async Task BeforeHostStartAsync()
    {
        await base.BeforeHostStartAsync();
        Provider!.DiscoveryIssuerOverride = "https://another-issuer.idp.test";
    }

    [Test]
    public async Task Read_WhenDiscoveryDocumentNamesAnotherIssuer_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader,
            Provider!.Mint("mismatch@example.test", options: new TokenOptions { Issuer = Provider.DiscoveryIssuer }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }
}
