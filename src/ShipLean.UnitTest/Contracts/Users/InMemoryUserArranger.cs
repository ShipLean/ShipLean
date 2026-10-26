using ShipLean.Application.Fakes.Users;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.UnitTest.Contracts.Users;

// Arranges users in the shared in-memory store the way the Postgres derivation arranges rows.
internal static class InMemoryUserArranger
{
    private static readonly Passwords Hasher = new();

    public static ArrangedUser Arrange(InMemoryUsers users, UserSpec spec)
    {
        var (name, email) = spec.NewIdentity();
        var now = TimeProvider.System.GetUtcNow();
        var hash = spec.Status == UserStatus.NoPassword ? null : Hasher.Hash(AppUserBuilder.ArrangedPassword);
        var user = users.Add(
            email, name, hash,
            mustChangePassword: spec.Status == UserStatus.OneTimePassword,
            lockedUntil: spec.Status == UserStatus.Locked ? now.AddHours(1) : null,
            disabledAt: spec.Status == UserStatus.Disabled ? now : null,
            isInstanceAdmin: spec.IsInstanceAdmin);
        foreach (var projectId in spec.ProjectIds)
        {
            users.AddMembership(projectId, user.Id);
        }
        return new ArrangedUser(user.Id, email, name);
    }
}
