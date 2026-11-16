using ShipLean.Contracts.DeviceSessions;
using ShipLean.Core;

namespace ShipLean.WebApi.Features.DeviceSessions;

// A stored session as the wire shows it, with its labels made in the viewer's zone (the zone passed in).
public static class DeviceSessionDtos
{
    public static DeviceSessionDto From(DeviceSessionRecord session, bool current, TimeProvider clock, TimeZoneInfo zone) => new(
        session.Id,
        session.Name,
        session.TokenPrefix,
        session.CreatedAt,
        DisplayDate.ShortDay(session.CreatedAt, clock.GetUtcNow(), zone),
        session.LastUsedAt,
        DisplayDate.Relative(session.LastUsedAt, clock.GetUtcNow(), zone),
        session.ExpiresAt,
        current);
}
