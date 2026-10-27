using ShipLean.WebApi.Features.WorkItemDetails.Attachments;

namespace ShipLean.WebApi.Features.Auth.Proxy;

// Where proxy identity is never read, even in proxy mode: /mcp, the agents' upload and download URLs and the machine OAuth endpoints keep their own scheme (an
// agent's bearer), so a proxy token header named Authorization can't be mistaken for one. The proxy is told to skip them.
public static class ProxySignInPaths
{
    public static IReadOnlyList<PathString> Skipped { get; } = ["/mcp", "/oauth/token", "/oauth/register", "/oauth/revoke", UploadServices.Path, DownloadServices.Path];

    public static bool IsSkipped(PathString path) => Skipped.Any(path.StartsWithSegments);
}
