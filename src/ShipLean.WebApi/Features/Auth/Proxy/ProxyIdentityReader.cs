using System.Collections.Concurrent;
using System.Net;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ShipLean.Application.Identity;

namespace ShipLean.WebApi.Features.Auth.Proxy;

// Reads the proxy's token (or trusted-peer header) into an identity. See IProxyIdentityReader for the rules.
// - Token mode: the token is the TokenHeader's value, or for Authorization only a "Bearer <token>" value, and
//   ProxyTokenVerifier verifies it.
// - Plain-header mode: EmailHeader (and NameHeader) count only when the raw TCP peer (ProxyPeer) is loopback or one of
//   the trusted proxies (SHIPLEAN_TRUSTED_PROXIES, the forwarded-headers rule). Forwarded headers never make a peer
//   trusted: the peer is the one that connected, captured before UseForwardedHeaders.
// A header sent more than once is refused. No log line carries a header value; untrusted-peer lines carry the address,
// at Warning once per peer per minute.
[RegisterSingleton(For = typeof(IProxyIdentityReader))]
public sealed class ProxyIdentityReader(
    ProxySignInSettings settings,
    ProxyTokenVerifier verifier,
    IOptions<ForwardedHeadersOptions> forwarding,
    TimeProvider time,
    ILogger<ProxyIdentityReader> logger) : IProxyIdentityReader
{
    private const string BearerPrefix = "Bearer ";

    // An untrusted peer's header is a Warning once per peer per interval and Debug otherwise, so anyone who can reach
    // the port can't flood the log. The table is cleared when it grows past MaxWarnedPeers.
    public static readonly TimeSpan UntrustedWarningInterval = TimeSpan.FromMinutes(1);

    private const int MaxWarnedPeers = 1024;

    private readonly ConcurrentDictionary<IPAddress, DateTimeOffset> _warnedPeers = new();

    public Task<ProxyIdentity?> ReadAsync(HttpRequest request, CancellationToken ct) => settings.Mode switch
    {
        ProxySignInMode.Token => ReadTokenAsync(request),
        ProxySignInMode.Header => Task.FromResult(ReadHeader(request)),
        _ => Task.FromResult<ProxyIdentity?>(null),
    };

    private async Task<ProxyIdentity?> ReadTokenAsync(HttpRequest request)
    {
        var header = settings.TokenHeader!;
        if (SingleValue(request, header) is not { } value)
        {
            return null;
        }
        if (!string.Equals(header, HeaderNames.Authorization, StringComparison.OrdinalIgnoreCase))
        {
            return await verifier.VerifyAsync(value);
        }
        if (value.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) && value[BearerPrefix.Length..].Trim() is { Length: > 0 } token)
        {
            return await verifier.VerifyAsync(token);
        }
        logger.LogWarning("Proxy sign-in ignored the {Header} header: it is not a Bearer token.", header);
        return null;
    }

    private ProxyIdentity? ReadHeader(HttpRequest request)
    {
        var emailHeader = settings.EmailHeader!;
        if (request.Headers[emailHeader].Count == 0)
        {
            return null;
        }
        var peer = ProxyPeer.Of(request);
        if (!IsTrusted(peer))
        {
            logger.Log(
                FirstInInterval(peer) ? LogLevel.Warning : LogLevel.Debug,
                "Proxy sign-in ignored the {Header} header from {Peer}: not loopback or a trusted proxy.", emailHeader, peer);
            return null;
        }
        if (SingleValue(request, emailHeader) is not { } value)
        {
            return null;
        }
        if (IdentityInput.Email(value) is not { } email)
        {
            logger.LogWarning("Proxy sign-in ignored the {Header} header: it is not a usable email.", emailHeader);
            return null;
        }
        var name = settings.NameHeader is { } nameHeader && request.Headers[nameHeader] is { Count: 1 } names
            ? IdentityInput.Name(names[0])
            : null;
        return new ProxyIdentity(email, name);
    }

    // The header's one non-blank value, trimmed; null when it is absent, blank or sent more than once (logged).
    private string? SingleValue(HttpRequest request, string header)
    {
        var values = request.Headers[header];
        if (values.Count == 0)
        {
            return null;
        }
        if (values.Count == 1 && values[0]?.Trim() is { Length: > 0 } value)
        {
            return value;
        }
        logger.LogWarning("Proxy sign-in ignored the {Header} header: it was blank or sent more than once.", header);
        return null;
    }

    // True when this peer has not been warned about within UntrustedWarningInterval. Two racing requests may both warn.
    private bool FirstInInterval(IPAddress? peer)
    {
        var key = peer ?? IPAddress.None;
        var now = time.GetUtcNow();
        if (_warnedPeers.TryGetValue(key, out var last) && now - last < UntrustedWarningInterval)
        {
            return false;
        }
        if (_warnedPeers.Count >= MaxWarnedPeers)
        {
            _warnedPeers.Clear();
        }
        _warnedPeers[key] = now;
        return true;
    }

    // Loopback, or a trusted proxy address or network; an IPv4-mapped IPv6 peer counts as its IPv4 address.
    private bool IsTrusted(IPAddress? peer)
    {
        if (peer is null)
        {
            return false;
        }
        IPAddress[] forms = peer.IsIPv4MappedToIPv6 ? [peer, peer.MapToIPv4()] : [peer];
        var options = forwarding.Value;
        return forms.Any(address => IPAddress.IsLoopback(address)
                                    || options.KnownProxies.Any(known => known.Equals(address)
                                                                         || (known.IsIPv4MappedToIPv6 && known.MapToIPv4().Equals(address)))
                                    || options.KnownIPNetworks.Any(network => network.Contains(address)));
    }
}
