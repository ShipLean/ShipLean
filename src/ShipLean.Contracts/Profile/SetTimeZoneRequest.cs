namespace ShipLean.Contracts.Profile;

// PUT /api/me/time-zone: the caller's time zone, an IANA id such as "Asia/Bangkok". A Browser value (the SPA's first-load
// capture) applies only while the caller has none, so it never overwrites a choice; a Profile value always applies. The
// answer is the caller's MeDto.
public sealed record SetTimeZoneRequest(string TimeZone, TimeZoneSource Source);
