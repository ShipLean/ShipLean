using ShipLean.Contracts;

namespace ShipLean.TestSupport.Identity;

// An IViewerZone for tests that build a store by hand: the zone is the one given (UTC when none), whatever the request.
public sealed class FixedViewerZone(TimeZoneInfo? zone = null) : IViewerZone
{
    public string? Id { get; } = zone is null ? null : zone.Id;

    public TimeZoneInfo Zone { get; } = zone ?? TimeZoneInfo.Utc;
}
