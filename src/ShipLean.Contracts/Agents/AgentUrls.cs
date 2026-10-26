namespace ShipLean.Contracts.Agents;

// The addresses an agent works with. Pure: callers pass instance_settings.base_url and the request's own origin.
public static class AgentUrls
{
    public const string McpPath = "/mcp";
    public const string AuthorizePath = "/oauth/authorize";
    public const string TokenPath = "/oauth/token";
    public const string RevokePath = "/oauth/revoke";
    public const string RegisterPath = "/oauth/register";
    public const string AuthorizationServerMetadataPath = "/.well-known/oauth-authorization-server";
    public const string ProtectedResourceMetadataPath = "/.well-known/oauth-protected-resource";

    // The SPA's consent page.
    public const string ConsentPagePath = "/authorize";

    // The origin (scheme, host, port) of base_url, or the request's own origin when base_url is blank. A path on base_url
    // is dropped: /mcp and /oauth/* are mapped at the root, so the issuer must be too. No trailing slash.
    public static string Issuer(string? baseUrl, string requestOrigin)
    {
        var source = (string.IsNullOrWhiteSpace(baseUrl) ? requestOrigin : baseUrl).Trim();
        return Uri.TryCreate(source, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority)
            : source.TrimEnd('/');
    }

    // The audience of every token: the /mcp URL.
    public static string Resource(string? baseUrl, string requestOrigin) => Issuer(baseUrl, requestOrigin) + McpPath;
}
