using Agoda.IoC.Core;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ShipLean.Application.Identity;

[RegisterSingleton]
public sealed class ProxyFirstAdmin(
    NpgsqlDataSource dataSource, UserSessions sessions, ProxySignInSettings settings, IConfiguration configuration, ILogger<ProxyFirstAdmin> logger)
    : IProxyFirstAdmin
{
    public async Task<bool> PromoteIfFirstAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        var admin = configuration[AdminBootstrap.EmailKey]?.Trim();
        if (!settings.IsProxy || string.IsNullOrEmpty(admin) || !string.Equals(admin, email?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var promoted = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE app_user SET is_admin = true WHERE id = @userId AND lower(email) = lower(@admin) AND NOT EXISTS (SELECT 1 FROM app_user WHERE is_admin)",
            new { userId, admin }, cancellationToken: cancellationToken)) > 0;
        if (!promoted)
        {
            return false;
        }
        sessions.Forget(userId);
        logger.LogWarning("Made {Email} the first instance admin at their first sign-in through the proxy; {EmailKey} has done its job.", email, AdminBootstrap.EmailKey);
        return true;
    }
}
