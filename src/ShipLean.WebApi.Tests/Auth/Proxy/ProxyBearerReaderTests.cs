using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Token mode with the token header set to Authorization: only a Bearer value is a token.
[TestFixture]
public class ProxyBearerReaderTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => ProxyIdentityProbeController.AddTo;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => Provider!.Settings(tokenHeader: "Authorization");

    [Test]
    public async Task Read_WhenHeaderIsAuthorizationBearer_ShouldReturnIdentity()
    {
        using var client = ProxyClient().WithToken("Authorization", Provider!.Mint("bearer@example.test", "Bea Rer"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBe(new ProxyIdentity("bearer@example.test", "Bea Rer"));
    }

    [Test]
    public async Task Read_WhenAuthorizationNotBearer_ShouldReturnNone()
    {
        using var client = ProxyClient().WithHeaders(("Authorization", $"Basic {Provider!.Mint("basic@example.test")}"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenAuthorizationIsBareToken_ShouldReturnNone()
    {
        using var client = ProxyClient().WithHeaders(("Authorization", Provider!.Mint("bare@example.test")));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }
}
