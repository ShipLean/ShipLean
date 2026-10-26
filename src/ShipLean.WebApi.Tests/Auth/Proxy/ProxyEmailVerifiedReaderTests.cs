using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Token mode and email_verified: a stated value must be true; a token that doesn't state it passes.
[TestFixture]
public class ProxyEmailVerifiedReaderTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => ProxyIdentityProbeController.AddTo;

    [Test]
    public async Task Read_WhenEmailVerifiedTrue_ShouldReturnIdentity()
    {
        (await ReadAsync("verified-bool@example.test", true)).ShouldBe(new ProxyIdentity("verified-bool@example.test", null));
    }

    [TestCase("true")]
    [TestCase("True")]
    public async Task Read_WhenEmailVerifiedTrueString_ShouldReturnIdentity(string value)
    {
        (await ReadAsync("verified-text@example.test", value)).ShouldBe(new ProxyIdentity("verified-text@example.test", null));
    }

    [Test]
    public async Task Read_WhenEmailVerifiedFalse_ShouldReturnNone()
    {
        (await ReadAsync("unverified-bool@example.test", false)).ShouldBeNull();
    }

    [TestCase("false")]
    [TestCase("yes")]
    [TestCase("")]
    public async Task Read_WhenEmailVerifiedOtherString_ShouldReturnNone(string value)
    {
        (await ReadAsync("unverified-text@example.test", value)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenEmailVerifiedNumberOrArray_ShouldReturnNone()
    {
        (await ReadAsync("unverified-number@example.test", 1)).ShouldBeNull();
        (await ReadAsync("unverified-array@example.test", new[] { true })).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenEmailVerifiedAbsent_ShouldReturnIdentity()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("unstated@example.test"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBe(new ProxyIdentity("unstated@example.test", null));
    }

    private async Task<ProxyIdentity?> ReadAsync(string email, object emailVerified)
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader,
            Provider!.Mint(email, options: new TokenOptions
            {
                ExtraClaims = new Dictionary<string, object> { [ProxyTokenVerifier.EmailVerifiedClaim] = emailVerified },
            }));
        return await ProxyIdentityProbeController.ReadAsync(client);
    }
}
