using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Token mode with an audience set: aud must contain it.
[TestFixture]
public class ProxyTokenAudienceReaderTests : ProxyApiFixture
{
    private const string Audience = "shiplean-tests";

    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => ProxyIdentityProbeController.AddTo;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => Provider!.Settings(Audience);

    [Test]
    public async Task Read_WhenAudienceMatches_ShouldReturnIdentity()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("aud@example.test", "Aud", new TokenOptions { Audience = Audience }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBe(new ProxyIdentity("aud@example.test", "Aud"));
    }

    [Test]
    public async Task Read_WhenAudienceConfiguredAndMissing_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("noaud@example.test"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenAudienceDiffers_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("otheraud@example.test", options: new TokenOptions { Audience = "another-app" }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }
}
