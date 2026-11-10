using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using ShipLean.Application.Identity;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Features.Auth.DeviceBearer;

// Where a request's device secret may come from, and where it counts. The secret is `Authorization: Bearer shl_dev_…`
// on /api/* and /hubs/*, or SignalR's `access_token` query value on /hubs/* only (a socket can't send headers from a
// browser). It is never read on the paths agents reach with their own credentials (/mcp, /oauth/*, /linear/graphql,
// upload and download URLs; ProxySignInPaths), whatever the mode. Any other bearer (an MCP access token) is not this
// scheme's.
public static class DeviceBearerRequest
{
    public const string AccessTokenParameter = "access_token";
    private const string BearerPrefix = "Bearer ";

    public static string? Secret(StringValues authorization)
    {
        if (authorization.Count != 1 || authorization[0] is not { } header || !header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var value = header[BearerPrefix.Length..].Trim();
        return DeviceSecret.HasShape(value) ? value : null;
    }

    // The device secret this request offers, or null when it offers none or is on a path that refuses it.
    public static string? Secret(HttpRequest request)
    {
        var path = request.Path;
        if (ProxySignInPaths.IsSkipped(path))
        {
            return null;
        }
        if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return Secret(request.Headers.Authorization);
        }
        if (path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            return Secret(request.Headers.Authorization) ?? QuerySecret(request.Query[AccessTokenParameter]);
        }
        return null;
    }

    // Whether the Session selector sends the request to DeviceBearer.
    public static bool Applies(HttpRequest request) => Secret(request) != null;

    private static string? QuerySecret(StringValues values)
        => values.Count == 1 && values[0] is { } value && DeviceSecret.HasShape(value) ? value : null;
}
