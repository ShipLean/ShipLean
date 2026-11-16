using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.UnitTest.Requests;

[TestFixture]
public class CurrentRequestTests
{
    [Test]
    public void Readers_WhenThereIsNoRequest_ShouldBeEmpty()
    {
        var current = CurrentRequests.Over(null);

        current.Exists.ShouldBeFalse();
        current.User.ShouldBeNull();
        current.Items.ShouldBeNull();
        current.Endpoint.ShouldBeNull();
        current.Path.ShouldBe(PathString.Empty);
        current.Header("X-Anything").ShouldBeNull();
    }

    [Test]
    public void Origin_WhenThereIsNoRequest_ShouldThrow()
    {
        var current = CurrentRequests.Over(null);

        Should.Throw<InvalidOperationException>(() => current.Origin);
    }

    [Test]
    public void Readers_WhenInARequest_ShouldReadThatRequest()
    {
        var context = new DefaultHttpContext
        {
            User = Principal("a"),
            Request = { Path = "/api/projects", Headers = { ["X-Thing"] = new[] { "first", "second" } } },
        };
        context.Items["key"] = 7;

        var current = CurrentRequests.Over(context);

        current.Exists.ShouldBeTrue();
        current.User.ShouldBeSameAs(context.User);
        current.Path.ShouldBe(new PathString("/api/projects"));
        current.Header("X-Thing").ShouldBe("first");
        current.Header("X-Missing").ShouldBeNull();
        current.Items.ShouldNotBeNull()["key"].ShouldBe(7);
    }

    [Test]
    public void Origin_WhenAProxyIsTrusted_ShouldUseTheForwardedHost()
    {
        var proxy = IPAddress.Parse("10.0.0.5");
        var context = new DefaultHttpContext { Request = { Scheme = "http", Host = new HostString("shiplean.svc:8080") } };
        context.Request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName] = "plan.example.com";
        context.Request.Headers[ForwardedHeadersDefaults.XForwardedProtoHeaderName] = "https";
        context.Features.Set(new ProxyPeer(proxy));
        var forwarding = new ForwardedHeadersOptions();
        forwarding.KnownProxies.Add(proxy);

        CurrentRequests.Accessor.HttpContext = context;
        var current = CurrentRequests.Create(forwarding);

        current.Origin.ShouldBe("https://plan.example.com");
        current.OriginClusterFallback.ShouldBeNull();
    }

    [Test]
    public void ActAs_WhenDisposed_ShouldPutBackThePreviousRequest()
    {
        var current = CurrentRequests.Over(null);
        var actor = Principal("runner");

        using (current.ActAs(actor))
        {
            current.Exists.ShouldBeTrue();
            current.User.ShouldBeSameAs(actor);
        }

        current.Exists.ShouldBeFalse();
    }

    private static ClaimsPrincipal Principal(string authenticationType)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], authenticationType));
}
