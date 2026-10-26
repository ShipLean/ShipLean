namespace ShipLean.Application.Identity;

// What a request needs to know about its user: whether the session still counts (not disabled, same security stamp),
// whether a password change is still required, and whether the user is an instance admin.
public sealed record UserSnapshot(Guid Id, bool IsAdmin, bool MustChangePassword, bool Disabled, string SecurityStamp);
