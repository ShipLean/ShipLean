using ShipLean.Contracts.DeviceSessions;
using ShipLean.Core;

namespace ShipLean.WebApi.Features.DeviceSessions;

// A stored session as the wire shows it, with its labels made in the viewer's zone (the clock's local zone).
public static class DeviceSessionDtos
{
    public static DeviceSessionDto From(DeviceSessionRecord session, bool current, TimeProvider clock) => new(
        session.Id,
        session.Name,
        session.TokenPrefix,
        session.CreatedAt,
        DisplayDate.ShortDay(session.CreatedAt, clock.GetUtcNow(), clock.LocalTimeZone),
        session.LastUsedAt,
        DisplayDate.Relative(session.LastUsedAt, clock),
        session.ExpiresAt,
        current);
}
