using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.Users;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Identity;

// The first instance admin, from the environment: when SHIPLEAN_ADMIN_EMAIL and SHIPLEAN_ADMIN_PASSWORD are both set
// and no instance admin exists yet, make that email an instance admin (creating the user if there is none, keeping
// an existing password if there is one) with that password. Once any instance admin exists it does nothing, so
// leaving the variables set is harmless; unset them after the first start anyway. In proxy mode the email alone is
// enough (the password is ignored with a warning): an existing user is promoted here, a new one at first sign-in.
// Registered after the migration service (AdminBootstrapServices), so app_user has its columns.
public sealed class AdminBootstrap(
    NpgsqlDataSource dataSource, IUserDirectory directory, Passwords passwords, IConfiguration configuration, ILogger<AdminBootstrap> logger)
    : IHostedService
{
    public const string EmailKey = "SHIPLEAN_ADMIN_EMAIL";
    public const string PasswordKey = "SHIPLEAN_ADMIN_PASSWORD";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = configuration[EmailKey]?.Trim();
        var password = configuration[PasswordKey];
        var proxy = ProxySignInSettings.Parse(configuration).IsProxy;
        if (proxy && !string.IsNullOrEmpty(password))
        {
            logger.LogWarning("{PasswordKey} is ignored in proxy mode: there are no passwords. Unset it.", PasswordKey);
        }
        if (string.IsNullOrEmpty(email) || (!proxy && string.IsNullOrEmpty(password)))
        {
            return;
        }
        if (!proxy)
        {
            try
            {
                Passwords.RequireAcceptable(password!);
            }
            catch (ApiException e)
            {
                throw new InvalidOperationException($"{PasswordKey}: {e.Message}", e);
            }
        }
        try
        {
            email = UserInput.RequireEmail(email);
        }
        catch (ApiException e)
        {
            throw new InvalidOperationException($"{EmailKey}: {e.Message}", e);
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (await connection.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS (SELECT 1 FROM app_user WHERE is_admin)", cancellationToken: cancellationToken)))
        {
            return;
        }
        if (proxy)
        {
            // The email alone is enough: promote the user if they exist (no password, no stamp change); a user who does
            // not exist yet is promoted at their first sign-in (ProxyFirstAdmin), never created here.
            if (await directory.FindByEmailAsync(email, cancellationToken) is { } existing)
            {
                var promoted = await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE app_user SET is_admin = true WHERE id = @id AND NOT EXISTS (SELECT 1 FROM app_user WHERE is_admin)",
                    new { id = existing.Id }, cancellationToken: cancellationToken)) > 0;
                if (promoted)
                {
                    logger.LogWarning("Made {Email} the first instance admin from the environment; unset {EmailKey} now.", email, EmailKey);
                }
            }
            return;
        }
        var user = await directory.FindByEmailAsync(email, cancellationToken)
                   ?? await directory.CreateAsync(email, email[..email.IndexOf('@')], cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE app_user SET is_admin = true, password_hash = coalesce(password_hash, @hash), security_stamp = @stamp
            WHERE id = @id AND NOT EXISTS (SELECT 1 FROM app_user WHERE is_admin)
            """, new { id = user.Id, hash = passwords.Hash(password!), stamp = AccountRules.NewStamp() }, cancellationToken: cancellationToken));
        logger.LogWarning("Made {Email} the first instance admin from the environment; unset {EmailKey} and {PasswordKey} now.", email, EmailKey, PasswordKey);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
