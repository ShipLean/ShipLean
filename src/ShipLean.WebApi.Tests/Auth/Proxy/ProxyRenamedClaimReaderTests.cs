using ShipLean.Application.Identity;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Token mode with SHIPLEAN_AUTH_PROXY_EMAIL_CLAIM = upn and SHIPLEAN_AUTH_PROXY_NAME_CLAIM = display_name.
[TestFixture]
public class ProxyRenamedClaimReaderTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => ProxyIdentityProbeController.AddTo;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => new Dictionary<string, string?>(Provider!.Settings())
    {
        [ProxySignInSettings.EmailClaimKey] = "upn",
        [ProxySignInSettings.NameClaimKey] = "display_name",
    };

    [Test]
    public async Task Read_WhenEmailClaimRenamed_ShouldReturnIdentity()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader,
            Provider!.Mint("upn@example.test", "U P N", new TokenOptions { EmailClaim = "upn", NameClaim = "display_name" }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBe(new ProxyIdentity("upn@example.test", "U P N"));
    }

    [Test]
    public async Task Read_WhenOnlyDefaultEmailClaimSent_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("default-claim@example.test", "Default"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }
}
