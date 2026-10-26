using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Auth;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Identity;

// POST /api/auth/login. Every refusal (unknown email, wrong password, no password, disabled, locked, too many attempts)
// is the same 401 with the same sentence, so the response never says whether an email has an account. Ten wrong
// passwords in a row lock the account for 15 minutes; an admin can unlock it sooner. Times come from the TimeProvider
// and are compared in C#.
[RegisterSingleton]
public sealed class SignInService(NpgsqlDataSource dataSource, Passwords passwords, SignInAttempts attempts, UserSessions sessions, TimeProvider clock)
{
    public const string Refusal = "Incorrect email or password, or the account is locked for now.";
    public const int LockoutFailures = 10;
    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    private sealed record Candidate(Guid Id, string? PasswordHash, bool Disabled, DateTimeOffset? LockedUntil);

    // The id of the user who is now signed in.
    public async Task<Guid> SignInAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        if (email.Length == 0 || password.Length == 0 || !attempts.TryRecord(email))
        {
            throw ApiException.Unauthorized(Refusal);
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // The row stays locked from the read to the commit, so parallel wrong passwords are counted one after another
        // and cannot all pass the lock check before the tenth locks the account.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var user = await connection.QuerySingleOrDefaultAsync<Candidate>(new CommandDefinition(
            """
            SELECT id, password_hash AS PasswordHash, disabled_at IS NOT NULL AS Disabled, locked_until AS LockedUntil
            FROM app_user WHERE lower(email) = lower(@email) FOR UPDATE
            """, new { email }, transaction, cancellationToken: cancellationToken));
        var now = clock.GetUtcNow();
        // Verify even when there is no user or no hash, so the refusal takes as long either way.
        var verified = passwords.Verify(user?.PasswordHash, password);
        if (user is null || user.Disabled || user.LockedUntil > now)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw ApiException.Unauthorized(Refusal);
        }
        if (!verified)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE app_user SET
                    locked_until = CASE WHEN failed_sign_in_count + 1 >= @limit THEN @lockedUntil ELSE locked_until END,
                    failed_sign_in_count = CASE WHEN failed_sign_in_count + 1 >= @limit THEN 0 ELSE failed_sign_in_count + 1 END
                WHERE id = @id
                """, new { id = user.Id, limit = LockoutFailures, lockedUntil = now + Lockout }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            throw ApiException.Unauthorized(Refusal);
        }
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE app_user SET failed_sign_in_count = 0, locked_until = NULL, last_sign_in_at = @now WHERE id = @id",
            new { id = user.Id, now }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        sessions.Forget(user.Id);
        return user.Id;
    }
}
