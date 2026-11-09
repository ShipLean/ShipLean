using System.Security.Claims;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Infrastructure;

// The surface of a web request: App when the authenticated principal carries the device-session claim (the request was made
// with a device bearer), else Web. Read from the principal, never from a header the client controls.
public static class UsageSurfaces
{
    public static UsageSurface Of(ClaimsPrincipal user)
        => user.HasClaim(c => c.Type == AuthPrincipal.DeviceSessionClaim) ? UsageSurface.App : UsageSurface.Web;
}
