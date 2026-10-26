using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Token mode with the JWKS URL set: the real reader verifies tokens the test identity provider mints. Every log line the
// host writes is captured, so a refused token can be shown never to reach one.
[TestFixture]
public class ProxyTokenReaderTests : ProxyApiFixture
{
    private readonly LogLines _logs = new();

    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => services =>
    {
        ProxyIdentityProbeController.AddTo(services);
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(new LoggerFactory([_logs]));
    };

    [Test]
    public async Task Read_WhenTokenValid_ShouldReturnEmailAndName()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint(" Mei.Valid@example.test ", "Mei Lin"));

        var identity = await ProxyIdentityProbeController.ReadAsync(client);

        identity.ShouldBe(new ProxyIdentity("Mei.Valid@example.test", "Mei Lin"));
    }

    [Test]
    public async Task Read_WhenNameClaimMissing_ShouldReturnNullName()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("noname@example.test"));

        var identity = await ProxyIdentityProbeController.ReadAsync(client);

        identity.ShouldBe(new ProxyIdentity("noname@example.test", null));
    }

    [Test]
    public async Task Read_WhenNoToken_ShouldReturnNone()
    {
        using var client = ProxyClient().WithoutIdentity();

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenSignedByForeignKey_ShouldReturnNone()
    {
        using var foreign = RSA.Create(2048);
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("forged@example.test", options: new TokenOptions { SigningKey = foreign }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenKidUnknown_ShouldReturnNone()
    {
        using var foreign = RSA.Create(2048);
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader,
            Provider!.Mint("unknown-kid@example.test", options: new TokenOptions { SigningKey = foreign, Kid = "someone-else" }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenIssuerWrong_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("issuer@example.test", options: new TokenOptions { Issuer = "https://other.idp.test" }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenExpiredBeyondSkew_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("expired@example.test", options: new TokenOptions { ExpiresIn = TimeSpan.FromMinutes(-10) }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    // Three minutes past exp: inside the library's 5-minute default skew, outside ours.
    [Test]
    public async Task Read_WhenExpiredThreeMinutesAgo_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("three@example.test", options: new TokenOptions { ExpiresIn = TimeSpan.FromMinutes(-3) }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenExpiredWithinSkew_ShouldReturnIdentity()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("skew@example.test", options: new TokenOptions { ExpiresIn = TimeSpan.FromSeconds(-30) }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBe(new ProxyIdentity("skew@example.test", null));
    }

    [Test]
    public async Task Read_WhenEmailClaimMissing_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint(null, "No Email"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenEmailInvalid_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint("not-an-email", "Bad Email"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenEmailClaimIsArray_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader,
            Provider!.Mint("first@example.test", options: new TokenOptions
            {
                ExtraClaims = new Dictionary<string, object> { ["email"] = new[] { "first@example.test", "second@example.test" } },
            }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenEmailClaimIsSingleElementArray_ShouldReturnNone()
    {
        using var client = ProxyClient().WithToken(
            TestIdentityProvider.DefaultTokenHeader,
            Provider!.Mint("only@example.test", options: new TokenOptions
            {
                ExtraClaims = new Dictionary<string, object> { ["email"] = new[] { "only@example.test" } },
            }));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    // alg none, no signature: refused by RequireSignedTokens and ValidAlgorithms whatever the claims say.
    [Test]
    public async Task Read_WhenTokenUnsigned_ShouldReturnNone()
    {
        var expires = TimeProvider.System.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds();
        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode(
            $"{{\"iss\":\"{TestIdentityProvider.Issuer}\",\"sub\":\"none@example.test\",\"email\":\"none@example.test\",\"exp\":{expires}}}");
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, $"{header}.{payload}.");

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenTokenRejected_ShouldNotLogTheToken()
    {
        var expired = Provider!.Mint("logged@example.test", options: new TokenOptions { ExpiresIn = TimeSpan.FromMinutes(-10) });
        using var foreignKey = RSA.Create(2048);
        var forged = Provider.Mint("forged-log@example.test", options: new TokenOptions { SigningKey = foreignKey });
        var malformed = "eyJhbGciOiJSUzI1NiJ9.not-json-at-all.c2lnbmF0dXJl";
        using var client = ProxyClient();

        (await ProxyIdentityProbeController.ReadAsync(client.WithToken(TestIdentityProvider.DefaultTokenHeader, expired))).ShouldBeNull();
        (await ProxyIdentityProbeController.ReadAsync(client.WithToken(TestIdentityProvider.DefaultTokenHeader, forged))).ShouldBeNull();
        (await ProxyIdentityProbeController.ReadAsync(client.WithToken(TestIdentityProvider.DefaultTokenHeader, malformed))).ShouldBeNull();

        var refusals = _logs.Lines.Where(line => line.Contains("refused a token", StringComparison.Ordinal)).ToList();
        refusals.Count.ShouldBeGreaterThanOrEqualTo(3);
        foreach (var part in expired.Split('.').Concat(forged.Split('.')).Append(malformed).Append("not-json-at-all"))
        {
            _logs.Lines.ShouldNotContain(line => line.Contains(part, StringComparison.Ordinal));
        }
    }

    // Every message any logger in the host writes, with its exception. Other reader test classes reuse it.
    internal sealed class LogLines : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _lines = new();

        public IReadOnlyList<string> Lines => [.. _lines];

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Logger(LogLines owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => owner._lines.Enqueue($"{logLevel} {category}: {formatter(state, exception)} {exception}");
        }
    }
}
