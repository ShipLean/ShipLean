namespace ShipLean.Contracts.Users;

// Account operations. Admin-only operations are guarded by the endpoint, not here. Every one throws NotFound for an
// unknown user id.
public interface IUserAccounts
{
    // Sets a new one-time password, requires a change at sign-in, rotates the stamp, clears the lockout; also sets up sign-in for a NoPassword user.
    Task<OneTimePasswordDto> IssueOneTimePasswordAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UnlockAsync(Guid userId, CancellationToken cancellationToken = default);

    // Throws Validation when disabling yourself or the last active instance admin; disabling rotates the stamp.
    Task SetDisabledAsync(Guid userId, bool disabled, CancellationToken cancellationToken = default);

    // Throws Validation when demoting yourself or the last active instance admin.
    Task SetInstanceAdminAsync(Guid userId, bool isInstanceAdmin, CancellationToken cancellationToken = default);

    // Throws Validation for a bad email, Conflict when another user has it; rotates the stamp.
    Task ChangeEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default);

    // The caller's own password; throws Validation when the current one is wrong or the new one is unacceptable. Clears the required change, rotates the stamp and returns it.
    Task<string> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    // Rotates the caller's stamp (every other session stops working) and returns it for the caller's own new session.
    Task<string> SignOutEverywhereElseAsync(CancellationToken cancellationToken = default);

    // Sets last_sign_in_at to now and nothing else (not the stamp, not the lockout).
    Task RecordSignInAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<string> GetSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default);
}
