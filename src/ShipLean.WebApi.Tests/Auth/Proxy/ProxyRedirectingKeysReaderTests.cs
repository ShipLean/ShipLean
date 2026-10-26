using ShipLean.Application.Identity;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// The JWKS URL answers 302 to the real JWKS: key fetches follow no redirects, so a valid token is still no identity.
[TestFixture]
public class ProxyRedirectingKeysReaderTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => ProxyIdentityProbeController.AddTo;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => new Dictionary<string, string?>(Provider!.Settings())
    {
        [ProxySignInSettings.JwksUrlKey] = Provider.RedirectingJwksUrl,
    };

    [Test]
    public async Task Read_WhenKeyUrlRedirects_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("redirected@example.test"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }
}
