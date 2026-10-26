using System.Net;
using System.Net.Http;
using Agoda.IoC.Core;
using ShipLean.Application.Http;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.PublicFetch;

// GETs an http/https address on the public internet for a link preview. The connection guard lives in the named clients'
// handler (PinnedConnector: one lookup, every answer must be public, connect to the vetted address), so it holds on every
// redirect too: each hop is a new request through the client for its host, and at most MaxRedirects are followed. An address
// a PreviewAllowHosts entry allows (its host on the default port, or the host:port it names: PreviewAllowEntry) goes through
// the client that allows any address. One deadline covers the whole fetch, and the body is read up to MaxBytes. Never throws
// for the address: a refused host is Private, anything else that fails is Unreachable.
[RegisterPerRequest]
public sealed class PublicFetcher(IHttpClientFactory httpClients, IIntegrationSettings integrations, PublicFetchOptions options)
{
    private IReadOnlyList<PreviewAllowEntry>? _allowed;

    public async Task<PublicFetchResult> FetchAsync(Uri address, string accept, CancellationToken cancellationToken)
    {
        if (!IsHttp(address))
        {
            return PublicFetchResult.Unreachable;
        }

        // Read before the deadline starts, so a slow settings store doesn't eat the fetch's time.
        _allowed ??= [.. (await integrations.GetAsync(cancellationToken)).PreviewAllowHosts.Select(PreviewAllowEntry.Parse).OfType<PreviewAllowEntry>()];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Timeout);
        try
        {
            return await FollowAsync(address, accept, deadline.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return IsRefusal(exception) ? PublicFetchResult.Private : PublicFetchResult.Unreachable;
        }
    }

    private async Task<PublicFetchResult> FollowAsync(Uri address, string accept, CancellationToken cancellationToken)
    {
        var current = address;
        // The first request and up to MaxRedirects more; a redirect after the last is too many.
        for (var redirects = 0; redirects <= options.MaxRedirects; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.TryAddWithoutValidation("Accept", accept);
            using var response = await ClientFor(current).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (IsRedirect(response.StatusCode))
            {
                if (response.Headers.Location is not { } location || !Uri.TryCreate(current, location, out var next) || !IsHttp(next))
                {
                    return PublicFetchResult.Unreachable;
                }
                current = next;
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                return PublicFetchResult.Unreachable;
            }

            var (body, truncated) = await ReadCappedAsync(response, cancellationToken);
            return new PublicFetchResult(PublicFetchStatus.Ok, current, body, response.Content.Headers.ContentType?.MediaType, truncated);
        }
        return PublicFetchResult.Unreachable;
    }

    private HttpClient ClientFor(Uri address)
        => httpClients.CreateClient(_allowed!.Any(entry => entry.Allows(address))
            ? PublicFetchOptions.AllowedHostClientName
            : PublicFetchOptions.HttpClientName);

    // Stops reading at MaxBytes; the rest of the body is never downloaded.
    private async Task<(byte[] Body, bool Truncated)> ReadCappedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            var room = options.MaxBytes - (int)buffer.Length;
            if (read > room)
            {
                await buffer.WriteAsync(chunk.AsMemory(0, room), cancellationToken);
                return (buffer.ToArray(), true);
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        return (buffer.ToArray(), false);
    }

    private static bool IsHttp(Uri address)
        => address.IsAbsoluteUri && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);

    private static bool IsRedirect(HttpStatusCode status)
        => status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    // SocketsHttpHandler wraps what the connect callback throws, so the refusal is somewhere down the chain.
    private static bool IsRefusal(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is AddressRefusedException)
            {
                return true;
            }
        }
        return false;
    }
}
