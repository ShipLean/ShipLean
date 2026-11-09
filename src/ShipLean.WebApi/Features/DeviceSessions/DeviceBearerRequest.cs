using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using ShipLean.Application.Features.DeviceSessions;

namespace ShipLean.WebApi.Features.DeviceSessions;

// Reads a device secret from an Authorization header: "Bearer " and a value with the secret's shape, or null. Any
// other bearer (an MCP access token) is not this scheme's.
public static class DeviceBearerRequest
{
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

    // Whether the Session selector sends the request to DeviceBearer: a device secret on /api/*.
    // TODO: hardened next: /hubs/* (with SignalR's access_token on hubs only), and the /api paths a bearer is refused on.
    public static bool Applies(PathString path, StringValues authorization)
        => path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) && Secret(authorization) != null;
}
