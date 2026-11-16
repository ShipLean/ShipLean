using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Core;

namespace ShipLean.Application.Identity;

// The viewer's zone from the zone claim ViewerZoneClaims puts on the request's user. No claim (no request user, or a
// user with no zone) or a stored id this host does not know (the database only checks its shape) reads as UTC, with Id null.
public sealed class ViewerZone(IServiceProvider services) : IViewerZone
{
    public string? Id => Resolve().Id;

    public TimeZoneInfo Zone => Resolve().Zone;

    private (string? Id, TimeZoneInfo Zone) Resolve()
    {
        var stored = services.GetService<IHttpContextAccessor>()?.HttpContext?.User.FindFirstValue(ViewerZoneClaims.ZoneClaim);
        return DisplayDate.TryZone(stored, out var zone) ? (zone.Id, zone) : (null, TimeZoneInfo.Utc);
    }
}
