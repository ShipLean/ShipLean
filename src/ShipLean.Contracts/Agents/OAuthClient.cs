namespace ShipLean.Contracts.Agents;

// A public OAuth client (no secret). ClientHost is the verified host of a metadata-document client, null when registered.
public sealed record OAuthClient(
    string ClientId,
    OAuthClientKind Kind,
    string ClientName,
    string? ClientHost,
    IReadOnlyList<string> RedirectUris)
{
    // True when the client's host was checked: a metadata-document client with a host. A registered client never is.
    public bool Verified => Kind == OAuthClientKind.MetadataDocument && ClientHost is not null;

    // Exact match against a registered redirect URI. The one exception is a loopback URI (http on 127.0.0.1, [::1] or
    // localhost, RFC 8252): it matches a registered loopback URI with the same host, path and query on any port.
    // A redirect with userinfo (http://x@127.0.0.1/cb) is never allowed, so it can't be made to look like another host.
    public bool AllowsRedirect(string redirectUri)
    {
        if (Uri.TryCreate(redirectUri, UriKind.Absolute, out var parsedRedirect) && parsedRedirect.UserInfo.Length > 0)
        {
            return false;
        }
        if (RedirectUris.Contains(redirectUri, StringComparer.Ordinal))
        {
            return true;
        }
        if (!TryLoopback(redirectUri, out var requested))
        {
            return false;
        }
        return RedirectUris.Any(registered => TryLoopback(registered, out var known)
            && string.Equals(known.Host, requested.Host, StringComparison.OrdinalIgnoreCase)
            && known.AbsolutePath == requested.AbsolutePath
            && known.Query == requested.Query);
    }

    // True for an absolute http URI on a loopback host, with no fragment and no userinfo.
    public static bool IsLoopback(string redirectUri) => TryLoopback(redirectUri, out _);

    private static bool TryLoopback(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttp
            && parsed.Fragment.Length == 0
            && parsed.UserInfo.Length == 0
            && parsed.Host is "127.0.0.1" or "[::1]" or "localhost")
        {
            uri = parsed;
            return true;
        }
        uri = null!;
        return false;
    }
}
