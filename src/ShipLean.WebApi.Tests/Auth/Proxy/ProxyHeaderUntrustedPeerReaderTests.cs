using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Plain-header mode reached through the machine's other address: a peer that is neither loopback nor a trusted proxy,
// as anyone who gets past the proxy would be. Its headers are ignored, forwarded ones included.
[TestFixture]
public class ProxyHeaderUntrustedPeerReaderTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool ListenOnAllInterfaces => true;

    private readonly ProxyTokenReaderTests.LogLines _logs = new();

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => services =>
    {
        ProxyIdentityProbeController.AddTo(services);
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(new LoggerFactory([_logs]));
    };

    protected override IReadOnlyDictionary<string, string?> ProxySettings => TestIdentityProvider.HeaderSettings();

    [Test]
    public async Task Read_WhenHeaderModeFromUntrustedPeer_ShouldReturnNone()
    {
        using var client = UntrustedClient().WithHeaders(("Remote-Email", "intruder@example.test"), ("Remote-Name", "In Truder"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenUntrustedPeerSpoofsForwardedFor_ShouldReturnNone()
    {
        using var client = UntrustedClient().WithHeaders(
            ("X-Forwarded-For", "127.0.0.1"), ("X-Original-For", "127.0.0.1:1234"), ("Remote-Email", "spoofer@example.test"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    // Every test here comes from the same peer within a minute, so the whole class warns about it once.
    [Test]
    public async Task Read_WhenUntrustedPeerRepeats_ShouldWarnOncePerMinute()
    {
        using var client = UntrustedClient().WithHeaders(("Remote-Email", "repeat@example.test"));

        for (var i = 0; i < 3; i++)
        {
            (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
        }

        var ignored = _logs.Lines.Where(line => line.Contains("not loopback or a trusted proxy", StringComparison.Ordinal)).ToList();
        ignored.Count(line => line.StartsWith("Warning", StringComparison.Ordinal)).ShouldBe(1);
        ignored.Count(line => line.StartsWith("Debug", StringComparison.Ordinal)).ShouldBeGreaterThanOrEqualTo(2);
    }

    private ProxyApiClient UntrustedClient()
        => ProxyClient(new Uri($"http://{LocalAddresses.OtherThanLoopback()}:{new Uri(Host.BaseUrl).Port}"));
}
