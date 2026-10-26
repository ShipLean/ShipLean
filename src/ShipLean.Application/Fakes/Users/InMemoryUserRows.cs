using ShipLean.Application.Identity;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Fakes.Users;

internal static class InMemoryUserRows
{
    // Call inside the lock.
    public static UserDto ToDto(this InMemoryUsers.Row row, TimeProvider clock) => new(
        row.Id, row.Name, row.Email, row.Initials, row.AvatarColour, null, row.IsAdmin,
        UserStatusRule.Derive(row.DisabledAt, row.PasswordHash is not null, row.LockedUntil, row.MustChangePassword, clock.GetUtcNow()));
}
