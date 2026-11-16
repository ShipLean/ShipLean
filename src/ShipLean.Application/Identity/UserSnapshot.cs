namespace ShipLean.Application.Identity;

// What a request needs to know about its user: whether the session still counts (not disabled, same security stamp),
// whether a password change is still required, whether the user is an instance admin, and the user's time zone (the
// stored IANA id, null when not set; ViewerZone reads it through the claim ViewerZoneClaims adds).
public sealed record UserSnapshot(Guid Id, bool IsAdmin, bool MustChangePassword, bool Disabled, string SecurityStamp, string? TimeZone = null);
