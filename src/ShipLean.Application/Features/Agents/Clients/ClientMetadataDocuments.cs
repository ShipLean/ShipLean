using System.Collections.Concurrent;
using System.Net.Http.Headers;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Clients;

// The real IClientMetadataDocuments. Every refusal answers null without saying why. The network guard (resolve once, refuse
// private addresses, connect to the vetted IP) lives in the named HttpClient's handler (PinnedConnector); here: https with
// a path only, no redirects (a non-200 is refused), a timeout, a size cap read incrementally, and the content rules of
// ClientMetadataParser. A refusal is cached at the floor too, so a bad client_id cannot make every /oauth/authorize fetch.
public sealed class ClientMetadataDocuments(IHttpClientFactory httpClients, TimeProvider clock, ClientMetadataOptions options)
    : IClientMetadataDocuments
{
    private readonly ConcurrentDictionary<string, Cached> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _fetches = new(options.MaxConcurrentFetches);

    private sealed record Cached(ClientMetadataDocument? Document, DateTimeOffset ExpiresAt);

    public async Task<ClientMetadataDocument?> FetchAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(clientId, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps
            || url.AbsolutePath.Length <= 1 || url.UserInfo.Length > 0 || url.Fragment.Length > 0)
        {
            return null;
        }
        var now = clock.GetUtcNow();
        if (_cache.TryGetValue(clientId, out var hit) && hit.ExpiresAt > now)
        {
            return hit.Document;
        }

        // At most MaxConcurrentFetches downloads at once, host-wide; one more is refused (and not cached) rather than queued.
        if (!await _fetches.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return null;
        }
        try
        {
            var (document, lifetime) = await DownloadAsync(clientId, url, cancellationToken);
            Remember(clientId, document, now, lifetime);
            return document is null ? null : document with { ExpiresAt = now + Clamp(lifetime) };
        }
        finally
        {
            _fetches.Release();
        }
    }

    private async Task<(ClientMetadataDocument? Document, TimeSpan Lifetime)> DownloadAsync(string clientId, Uri url, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await httpClients.CreateClient(ClientMetadataOptions.HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != System.Net.HttpStatusCode.OK || response.Content.Headers.ContentLength > options.MaxBytes)
            {
                return (null, options.MinCache);
            }
            var body = await ReadCappedAsync(response, timeout.Token);
            var client = body is null ? null : ClientMetadataParser.Parse(clientId, url, body);
            var lifetime = Lifetime(response, clock.GetUtcNow());
            return client is null ? (null, options.MinCache) : (new ClientMetadataDocument(client, default), lifetime);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (null, options.MinCache);
        }
    }

    private async Task<byte[]?> ReadCappedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > options.MaxBytes)
            {
                return null;
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        return buffer.ToArray();
    }

    // max-age, else Expires; no-store, no-cache and nothing at all give the floor.
    private TimeSpan Lifetime(HttpResponseMessage response, DateTimeOffset now)
    {
        var control = response.Headers.CacheControl;
        if (control is { NoStore: true } or { NoCache: true })
        {
            return options.MinCache;
        }
        if (control?.MaxAge is { } maxAge)
        {
            return maxAge;
        }
        return response.Content.Headers.Expires is { } expires ? expires - now : options.MinCache;
    }

    private TimeSpan Clamp(TimeSpan lifetime) => TimeSpan.FromTicks(Math.Clamp(lifetime.Ticks, options.MinCache.Ticks, options.MaxCache.Ticks));

    // When full, drop expired entries, then the one nearest expiry, so a flood of bad ids cannot keep new answers out.
    private void Remember(string clientId, ClientMetadataDocument? document, DateTimeOffset now, TimeSpan lifetime)
    {
        if (_cache.Count >= options.MaxCachedUrls && !_cache.ContainsKey(clientId))
        {
            foreach (var expired in _cache.Where(entry => entry.Value.ExpiresAt <= now).Select(entry => entry.Key).ToList())
            {
                _cache.TryRemove(expired, out _);
            }
            if (_cache.Count >= options.MaxCachedUrls && _cache.MinBy(entry => entry.Value.ExpiresAt).Key is { } nearest)
            {
                _cache.TryRemove(nearest, out _);
            }
        }
        var expiresAt = now + Clamp(lifetime);
        _cache[clientId] = new Cached(document is null ? null : document with { ExpiresAt = expiresAt }, expiresAt);
    }
}
