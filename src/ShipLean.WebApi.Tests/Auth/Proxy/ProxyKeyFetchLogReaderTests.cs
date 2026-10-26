using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Identity;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// A JWKS URL that answers 404 and carries a secret in its query: the warning names host, path and status, never the
// query or the response body.
[TestFixture]
public class ProxyKeyFetchLogReaderTests : ProxyApiFixture
{
    private const string Secret = "s3cr3t-in-the-query";
    private readonly ProxyTokenReaderTests.LogLines _logs = new();

    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => services =>
    {
        ProxyIdentityProbeController.AddTo(services);
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(new LoggerFactory([_logs]));
    };

    protected override IReadOnlyDictionary<string, string?> ProxySettings => new Dictionary<string, string?>(Provider!.Settings())
    {
        [ProxySignInSettings.JwksUrlKey] = $"{Provider.DiscoveryIssuer}/no-such-jwks?key={Secret}",
    };

    [Test]
    public async Task Read_WhenKeyFetchFails_ShouldLogHostPathAndStatusOnly()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("fetchlog@example.test"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();

        var port = new Uri(Provider.DiscoveryIssuer).Port;
        _logs.Lines.ShouldContain(line => line.Contains($"could not be fetched (The signing-key endpoint 127.0.0.1:{port}/no-such-jwks answered 404.)", StringComparison.Ordinal));
        _logs.Lines.ShouldNotContain(line => line.Contains(Secret, StringComparison.Ordinal));
    }
}
