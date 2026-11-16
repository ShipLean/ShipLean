using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace ShipLean.Application.Identity;

// Adds the user's stored time zone to every authenticated principal with a user id (cookie, device bearer and MCP alike),
// read through the one-minute UserSessions cache, for ViewerZone. The incoming principal is never changed: the result is
// a clone with the zone claim on an identity of its own, so a cookie the handler re-issues (sliding expiration) never
// carries a zone that would go stale. A request can authenticate more than once (a policy scheme forwards, and both runs
// transform), so any earlier zone identity is dropped first and the claim appears once.
public sealed class ViewerZoneClaims(UserSessions sessions) : IClaimsTransformation
{
    public const string AuthenticationType = "ShipLean.ViewerZone";
    public const string ZoneClaim = "shiplean:time-zone";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return principal;
        }

        var user = await sessions.GetAsync(userId);
        var identities = principal.Clone().Identities.Where(identity => identity.AuthenticationType != AuthenticationType).ToList();
        var zone = new ClaimsIdentity(AuthenticationType);
        if (user?.TimeZone is { } id)
        {
            zone.AddClaim(new Claim(ZoneClaim, id));
        }
        identities.Add(zone);
        return new ClaimsPrincipal(identities);
    }
}
