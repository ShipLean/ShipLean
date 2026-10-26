using ShipLean.Application.Features.Users;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Fakes.Users;

public sealed class InMemoryUserAccounts(InMemoryUsers users, ICurrentUser currentUser, Passwords? hasher = null) : IUserAccounts
{
    private readonly Passwords passwords = hasher ?? new Passwords();

    public Task<OneTimePasswordDto> IssueOneTimePasswordAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var oneTime = Passwords.OneTime();
        var hash = passwords.Hash(oneTime);
        Write(userId, row =>
        {
            row.PasswordHash = hash;
            row.MustChangePassword = true;
            row.SecurityStamp = AccountRules.NewStamp();
            row.FailedSignInCount = 0;
            row.LockedUntil = null;
        });
        return Task.FromResult(new OneTimePasswordDto(userId, oneTime));
    }

    public Task UnlockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Write(userId, row =>
        {
            row.FailedSignInCount = 0;
            row.LockedUntil = null;
        });
        return Task.CompletedTask;
    }

    public Task SetDisabledAsync(Guid userId, bool disabled, CancellationToken cancellationToken = default)
    {
        Write(userId, row =>
        {
            if (disabled)
            {
                AccountRules.CheckDisable(userId, currentUser.Id, IsActiveAdmin(row), AnotherActiveAdmin(userId));
                row.DisabledAt ??= users.Clock.GetUtcNow();
                row.SecurityStamp = AccountRules.NewStamp();
            }
            else
            {
                row.DisabledAt = null;
            }
        });
        return Task.CompletedTask;
    }

    public Task SetInstanceAdminAsync(Guid userId, bool isInstanceAdmin, CancellationToken cancellationToken = default)
    {
        Write(userId, row =>
        {
            if (!isInstanceAdmin)
            {
                AccountRules.CheckDemote(userId, currentUser.Id, IsActiveAdmin(row), AnotherActiveAdmin(userId));
            }
            row.IsAdmin = isInstanceAdmin;
        });
        return Task.CompletedTask;
    }

    public Task ChangeEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default)
    {
        var clean = UserInput.RequireEmail(email);
        Write(userId, row =>
        {
            if (users.Rows.Any(r => r.Id != userId && string.Equals(r.Email, clean, StringComparison.OrdinalIgnoreCase)))
            {
                throw ApiException.Conflict($"A user with the email {clean} already exists.");
            }
            row.Email = clean;
            row.SecurityStamp = AccountRules.NewStamp();
        });
        return Task.CompletedTask;
    }

    public Task<string> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var stamp = AccountRules.NewStamp();
        Write(currentUser.Id, row =>
        {
            if (!passwords.Verify(row.PasswordHash, currentPassword ?? string.Empty))
            {
                throw ApiException.Validation("The current password is wrong.");
            }
            Passwords.RequireAcceptable(newPassword);
            row.PasswordHash = passwords.Hash(newPassword);
            row.MustChangePassword = false;
            row.SecurityStamp = stamp;
        });
        return Task.FromResult(stamp);
    }

    public Task<string> SignOutEverywhereElseAsync(CancellationToken cancellationToken = default)
    {
        var stamp = AccountRules.NewStamp();
        Write(currentUser.Id, row => row.SecurityStamp = stamp);
        return Task.FromResult(stamp);
    }

    public Task RecordSignInAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (users.Gate)
        {
            var row = users.Rows.FirstOrDefault(r => r.Id == userId) ?? throw AccountRules.UserNotFound(userId);
            row.LastSignInAt = users.Clock.GetUtcNow();
        }
        return Task.CompletedTask;
    }

    public Task<string> GetSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (users.Gate)
        {
            return Task.FromResult((users.Rows.FirstOrDefault(r => r.Id == userId) ?? throw AccountRules.UserNotFound(userId)).SecurityStamp);
        }
    }

    // Runs the change under the lock on the user's row, then stamps updated_at; NotFound for an unknown id.
    private void Write(Guid userId, Action<InMemoryUsers.Row> change)
    {
        lock (users.Gate)
        {
            var row = users.Rows.FirstOrDefault(r => r.Id == userId) ?? throw AccountRules.UserNotFound(userId);
            change(row);
            row.UpdatedAt = users.Clock.GetUtcNow();
        }
    }

    private static bool IsActiveAdmin(InMemoryUsers.Row row) => row.IsAdmin && row.DisabledAt is null;

    private bool AnotherActiveAdmin(Guid userId) => users.Rows.Any(r => r.Id != userId && IsActiveAdmin(r));
}
