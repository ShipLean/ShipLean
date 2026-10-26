using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Agents.Clients;

// Which redirect URIs a client may declare (RFC 8252): https; http on a loopback host (section 7.3); or a private-use
// scheme such as cursor://anysphere.cursor-retrieval/oauth/callback (section 7.1). No userinfo, no fragment.
// The SPA mirrors the scheme rule in features/agents/authorize/returnAddress.ts: change both together.
public static partial class RedirectUriRules
{
    public const int MaxRedirects = 10;

    // Schemes that are never a private-use redirect: http(s) keep their own rules, the rest can run script or read local data.
    private static readonly HashSet<string> BlockedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "javascript", "data", "file", "vbscript", "blob", "about", "ws", "wss", "ftp", "filesystem",
        "view-source", "mailto", "tel", "ms-msdt", "search-ms", "search", "ms-officecmd", "intent", "chrome", "chrome-extension", "jar",
    };

    public static bool IsAcceptable(string? redirect)
    {
        if (redirect is null || redirect != redirect.Trim() || redirect.Any(char.IsControl) || !Uri.TryCreate(redirect, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0
            || redirect.Contains('#', StringComparison.Ordinal))
        {
            return false;
        }
        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return uri.Host.Length > 0;
        }
        return uri.Scheme == Uri.UriSchemeHttp
            ? uri.Host.Length > 0 && ShipLean.Contracts.Agents.OAuthClient.IsLoopback(redirect)
            : IsPrivateUseScheme(uri.Scheme);
    }

    public static bool IsPrivateUseScheme(string scheme) => SchemePattern().IsMatch(scheme) && !BlockedSchemes.Contains(scheme);

    // What the consent page shows as the place the person returns to: the host for http(s), scheme://host for a private-use scheme.
    public static string Describe(Uri redirect)
        => redirect.Scheme is "http" or "https" ? redirect.Host : $"{redirect.Scheme}://{redirect.Host}";

    [GeneratedRegex("^[a-z][a-z0-9+.-]*\\z", RegexOptions.IgnoreCase)]
    private static partial Regex SchemePattern();
}
