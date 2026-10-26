using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Features.Users;

// Every change to a user ends with sessions.Forget(userId), so the next request re-reads the row and a disabled user,
// a rotated stamp or a demoted admin takes effect at once instead of after the one-minute cache.
public sealed class UserAccounts(NpgsqlDataSource dataSource, ICurrentUser currentUser, Passwords passwords, TimeProvider clock, UserSessions sessions) : IUserAccounts
{
    public async Task<OneTimePasswordDto> IssueOneTimePasswordAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var oneTime = Passwords.OneTime();
        await UpdateAsync(
            userId,
            """
            UPDATE app_user SET security_stamp = @stamp, must_change_password = true, password_hash = @hash,
                                failed_sign_in_count = 0, locked_until = NULL, updated_at = @now
            WHERE id = @userId
            """,
            new { hash = passwords.Hash(oneTime), stamp = AccountRules.NewStamp() },
            cancellationToken);
        return new OneTimePasswordDto(userId, oneTime);
    }

    public Task UnlockAsync(Guid userId, CancellationToken cancellationToken = default)
        => UpdateAsync(userId, "UPDATE app_user SET failed_sign_in_count = 0, locked_until = NULL, updated_at = @now WHERE id = @userId", new { }, cancellationToken);

    public async Task SetDisabledAsync(Guid userId, bool disabled, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var target = await LockAdminsAndLoadAsync(connection, transaction, userId, cancellationToken);
        if (disabled)
        {
            AccountRules.CheckDisable(userId, currentUser.Id, target.IsActiveAdmin, target.AnotherActiveAdminExists);
        }
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE app_user SET
                disabled_at = CASE WHEN @disabled THEN coalesce(disabled_at, @now) ELSE NULL END,
                security_stamp = CASE WHEN @disabled THEN @stamp ELSE security_stamp END,
                updated_at = @now
            WHERE id = @userId
            """,
            new { userId, disabled, now = clock.GetUtcNow(), stamp = AccountRules.NewStamp() },
            transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        sessions.Forget(userId);
    }

    public async Task SetInstanceAdminAsync(Guid userId, bool isInstanceAdmin, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var target = await LockAdminsAndLoadAsync(connection, transaction, userId, cancellationToken);
        if (!isInstanceAdmin)
        {
            AccountRules.CheckDemote(userId, currentUser.Id, target.IsActiveAdmin, target.AnotherActiveAdminExists);
        }
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE app_user SET is_admin = @isInstanceAdmin, updated_at = @now WHERE id = @userId",
            new { userId, isInstanceAdmin, now = clock.GetUtcNow() }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        sessions.Forget(userId);
    }

    public async Task ChangeEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default)
    {
        var clean = UserInput.RequireEmail(email);
        try
        {
            await UpdateAsync(userId, "UPDATE app_user SET email = @email, security_stamp = @stamp, updated_at = @now WHERE id = @userId", new { email = clean, stamp = AccountRules.NewStamp() }, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ApiException.Conflict($"A user with the email {clean} already exists.");
        }
    }

    public async Task<string> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var hash = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT password_hash FROM app_user WHERE id = @userId", new { userId }, cancellationToken: cancellationToken));
        if (!passwords.Verify(hash, currentPassword ?? string.Empty))
        {
            throw ApiException.Validation("The current password is wrong.");
        }
        Passwords.RequireAcceptable(newPassword);
        var stamp = AccountRules.NewStamp();
        await UpdateAsync(
            userId, "UPDATE app_user SET security_stamp = @stamp, must_change_password = false, password_hash = @hash, updated_at = @now WHERE id = @userId AND disabled_at IS NULL",
            new { hash = passwords.Hash(newPassword), stamp }, cancellationToken);
        return stamp;
    }

    public async Task<string> SignOutEverywhereElseAsync(CancellationToken cancellationToken = default)
    {
        var stamp = AccountRules.NewStamp();
        await UpdateAsync(currentUser.Id, "UPDATE app_user SET security_stamp = @stamp, updated_at = @now WHERE id = @userId", new { stamp }, cancellationToken);
        return stamp;
    }

    public Task RecordSignInAsync(Guid userId, CancellationToken cancellationToken = default)
        => UpdateAsync(userId, "UPDATE app_user SET last_sign_in_at = @now WHERE id = @userId", new { }, cancellationToken);

    public async Task<string> GetSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                "SELECT security_stamp FROM app_user WHERE id = @userId", new { userId }, cancellationToken: cancellationToken))
            ?? throw AccountRules.UserNotFound(userId);
    }

    // One UPDATE of a single user (@userId, @now are added here); NotFound when no row matched.
    private async Task UpdateAsync(Guid userId, string sql, object parameters, CancellationToken cancellationToken)
    {
        var values = new DynamicParameters(parameters);
        values.Add("userId", userId);
        values.Add("now", clock.GetUtcNow());
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            sql, values, cancellationToken: cancellationToken));
        if (changed == 0)
        {
            throw AccountRules.UserNotFound(userId);
        }
        sessions.Forget(userId);
    }

    private sealed record Target(bool IsActiveAdmin, bool AnotherActiveAdminExists);

    // Locks every admin row, so two admins demoting each other at once cannot leave none, then reads the target.
    private static async Task<Target> LockAdminsAndLoadAsync(
        NpgsqlConnection connection, DbTransaction transaction, Guid userId, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT id FROM app_user WHERE is_admin FOR UPDATE", transaction: transaction, cancellationToken: cancellationToken));
        return await connection.QuerySingleOrDefaultAsync<Target>(new CommandDefinition(
                """
                SELECT (is_admin AND disabled_at IS NULL) AS isActiveAdmin,
                       EXISTS (SELECT 1 FROM app_user other WHERE other.is_admin AND other.disabled_at IS NULL AND other.id <> app_user.id) AS anotherActiveAdminExists
                FROM app_user WHERE id = @userId
                """,
                new { userId }, transaction, cancellationToken: cancellationToken))
            ?? throw AccountRules.UserNotFound(userId);
    }
}
