using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// ProxyApiFixture itself: the real schemes, the fake reader, and the identity provider started before the host.
[TestFixture]
public class ProxyApiFixtureTests : ProxyApiFixture
{
    // TestAuthentication would sign X-Test-User in as Joel; with only the real schemes it is just a header.
    [Test]
    public async Task ProxyApiFixture_WhenStarted_ShouldRunRealSchemes()
    {
        using var client = ProxyClient().WithHeaders((TestAuthentication.UserHeader, TestUser.Joel.Id!.Value.ToString()));

        using var response = await client.GetAsync("/api/me");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
    }

    [Test]
    public void ProxyApiFixture_WhenFakeReaderSwapped_ShouldResolveFake()
    {
        Host.Services.GetRequiredService<IProxyIdentityReader>().ShouldBeOfType<FakeProxyIdentityReader>();
    }

    // The provider must be running, with its URLs known, before the host reads its settings.
    [TestFixture]
    public class WithIdentityProvider : ProxyApiFixture
    {
        protected override bool UseIdentityProvider => true;

        [Test]
        public void ProxyApiFixture_WhenIdentityProviderUsed_ShouldStartItBeforeTheHost()
        {
            var configuration = Host.Services.GetRequiredService<IConfiguration>();

            configuration["SHIPLEAN_AUTH_PROXY_TOKEN_JWKS_URL"].ShouldBe(Provider!.JwksUrl);
            configuration["SHIPLEAN_AUTH_PROXY_TOKEN_ISSUER"].ShouldBe(TestIdentityProvider.Issuer);
        }
    }
}
