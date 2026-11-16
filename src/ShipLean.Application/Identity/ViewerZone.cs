using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Core;

namespace ShipLean.Application.Identity;

// The viewer's zone from the zone claim ViewerZoneClaims puts on the request's user. No claim (no request user, or a
// user with no zone) or a stored id this host does not know (the database only checks its shape) reads as UTC, with Id null.
// Resolved once per request and principal: Id and Zone both read the answer kept on the request, so a read of each
// looks the claim up and checks the id once. A different principal on the same request (a re-authentication) resolves again.
public sealed class ViewerZone(IServiceProvider services) : IViewerZone
{
    private static readonly object ItemsKey = new();

    public string? Id => Resolve().Id;

    public TimeZoneInfo Zone => Resolve().Zone;

    private Resolved Resolve()
    {
        var context = services.GetService<IHttpContextAccessor>()?.HttpContext;
        if (context is null)
        {
            return Resolved.Utc;
        }

        var principal = context.User;
        if (context.Items.TryGetValue(ItemsKey, out var kept) && kept is Resolved cached && ReferenceEquals(cached.Principal, principal))
        {
            return cached;
        }

        var stored = principal.FindFirstValue(ViewerZoneClaims.ZoneClaim);
        var resolved = DisplayDate.TryZone(stored, out var zone) ? new Resolved(principal, zone.Id, zone) : new Resolved(principal, null, TimeZoneInfo.Utc);
        context.Items[ItemsKey] = resolved;
        return resolved;
    }

    private sealed record Resolved(ClaimsPrincipal? Principal, string? Id, TimeZoneInfo Zone)
    {
        public static readonly Resolved Utc = new(null, null, TimeZoneInfo.Utc);
    }
}
