using Agoda.IoC.Core;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.Users;
using ShipLean.Core.People;

namespace ShipLean.Application.Identity;

// Which user a verified Google identity is. The subject decides once it is linked, so a later change of email at Google
// doesn't matter. A subject seen for the first time links to the user with that email, or creates one: anyone with a
// Google account may sign in. An email already linked to another subject is refused, because an address that changed
// hands at Google must not reach the account of whoever held it before.
//
// A password lockout doesn't apply and isn't cleared: it stops password guessing, which anyone can trigger against
// someone else. A one-time password does not survive: its only purpose is getting in, the change-password screen needs
// the one just typed, and leaving it would leave a password someone else knows.
[RegisterSingleton]
public sealed class GoogleAccounts(NpgsqlDataSource dataSource, UserSessions sessions, TimeProvider clock, ILogger<GoogleAccounts> logger)
{
    private const string FindUser =
        """
        SELECT id, google_subject AS GoogleSubject, disabled_at IS NOT NULL AS Disabled, must_change_password AS MustChangePassword
        FROM app_user
        WHERE google_subject = @subject OR lower(email) = lower(@email)
        ORDER BY google_subject = @subject DESC NULLS LAST
        LIMIT 1
        FOR UPDATE
        """;

    private sealed record Candidate(Guid Id, string? GoogleSubject, bool Disabled, bool MustChangePassword);

    // The id of the user to sign in, or null when this identity is refused. The email must already be verified and clean.
    public async Task<Guid?> ResolveAsync(string subject, string email, string? name, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var user = await FindAsync(connection, transaction, subject, email, cancellationToken);
        if (user is null)
        {
            if (await CreateAsync(connection, transaction, subject, email, name, now, cancellationToken) is { } created)
            {
                await transaction.CommitAsync(cancellationToken);
                return created;
            }
            // Someone else's first sign-in with this email or subject committed between the read and the insert.
            user = await FindAsync(connection, transaction, subject, email, cancellationToken)
                   ?? throw new InvalidOperationException("The user row vanished between the insert and the read.");
        }
        if (user.GoogleSubject is not null && user.GoogleSubject != subject)
        {
            logger.LogWarning("Google sign-in refused for user {UserId}: the account is linked to a different Google identity", user.Id);
            return null;
        }
        if (user.Disabled)
        {
            logger.LogInformation("Google sign-in refused for user {UserId}: the account is disabled", user.Id);
            return null;
        }
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE app_user SET google_subject = @subject, last_sign_in_at = @now,
                updated_at = CASE WHEN google_subject IS NULL THEN @now ELSE updated_at END
            WHERE id = @id
            """,
            new { id = user.Id, subject, now }, transaction, cancellationToken: cancellationToken));
        if (user.MustChangePassword)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE app_user SET security_stamp = @stamp, must_change_password = false, password_hash = @hash, updated_at = @now WHERE id = @id",
                new { id = user.Id, now, stamp = AccountRules.NewStamp(), hash = (string?)null }, transaction, cancellationToken: cancellationToken));
        }
        await transaction.CommitAsync(cancellationToken);
        sessions.Forget(user.Id);
        return user.Id;
    }

    private static Task<Candidate?> FindAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string subject, string email, CancellationToken cancellationToken)
        => connection.QuerySingleOrDefaultAsync<Candidate>(new CommandDefinition(FindUser, new { subject, email }, transaction, cancellationToken: cancellationToken));

    // The new user's id, or null when another row already holds the email or the subject.
    private static Task<Guid?> CreateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string subject, string email, string? name, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cleanName = UserInput.UsableName(name) ?? UserInput.NameFromEmail(email);
        return connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            INSERT INTO app_user (id, email, name, initials, avatar_colour, security_stamp, google_subject, last_sign_in_at, created_at, updated_at)
            VALUES (@id, @email, @name, @initials, @colour, @stamp, @subject, @now, @now, @now)
            ON CONFLICT DO NOTHING
            RETURNING id
            """,
            new
            {
                id = Guid.CreateVersion7(),
                email,
                name = cleanName,
                initials = Initials.From(cleanName),
                colour = AvatarPalette.For(email),
                stamp = AccountRules.NewStamp(),
                subject,
                now,
            },
            transaction, cancellationToken: cancellationToken));
    }
}
