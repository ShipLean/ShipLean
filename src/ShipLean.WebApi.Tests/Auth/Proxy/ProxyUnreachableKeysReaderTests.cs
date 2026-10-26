using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Token mode whose JWKS URL answers nothing (port 9): every token is no identity, and the instance keeps running.
[TestFixture]
public class ProxyUnreachableKeysReaderTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => ProxyIdentityProbeController.AddTo;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => LiteralTokenSettings;

    [Test]
    public async Task Read_WhenKeyEndpointUnreachable_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("unreachable@example.test"));

        var first = await ProxyIdentityProbeController.ReadAsync(client);
        var second = await ProxyIdentityProbeController.ReadAsync(client);
        using var health = await client.WithoutIdentity().GetAsync("/api/health");

        first.ShouldBeNull();
        second.ShouldBeNull();
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
