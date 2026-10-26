using ShipLean.Contracts.Users;

namespace ShipLean.Application.Identity;

// The derived UserStatus, first match wins: disabled, noPassword, locked, oneTimePassword, active. Derive is the rule
// over row fields; Sql is the same rule as a CASE expression over app_user's columns (unqualified, so use it in a
// query with one app_user in scope), so a read model can select the status without loading the row. Sql needs the
// parameter @now = TimeProvider.GetUtcNow(), so the database and C# agree on "locked" under any clock.
public static class UserStatusRule
{
    public const string Sql =
        """
        CASE WHEN disabled_at IS NOT NULL THEN 'disabled'
             WHEN password_hash IS NULL THEN 'noPassword'
             WHEN locked_until > @now THEN 'locked'
             WHEN must_change_password THEN 'oneTimePassword'
             ELSE 'active' END
        """;

    public static UserStatus Derive(DateTimeOffset? disabledAt, bool hasPassword, DateTimeOffset? lockedUntil, bool mustChangePassword, DateTimeOffset now)
    {
        if (disabledAt is not null)
        {
            return UserStatus.Disabled;
        }
        if (!hasPassword)
        {
            return UserStatus.NoPassword;
        }
        if (lockedUntil > now)
        {
            return UserStatus.Locked;
        }
        return mustChangePassword ? UserStatus.OneTimePassword : UserStatus.Active;
    }

    // Reads what Sql returns (camelCase text) back into the enum.
    public static UserStatus Parse(string sqlValue) => Enum.Parse<UserStatus>(sqlValue, ignoreCase: true);
}
