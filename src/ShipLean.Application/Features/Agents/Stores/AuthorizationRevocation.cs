using System.Data.Common;
using Dapper;

namespace ShipLean.Application.Features.Agents.Stores;

// Revoking an authorization ends its tokens too. Nothing is deleted: rows keep a revoked_at.
internal static class AuthorizationRevocation
{
    public static async Task RevokeAsync(DbConnection connection, DbTransaction? transaction, Guid authorizationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE oauth_authorization SET revoked_at = coalesce(revoked_at, @now) WHERE id = @authorizationId",
            new { authorizationId, now }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE oauth_token SET revoked_at = coalesce(revoked_at, @now) WHERE authorization_id = @authorizationId",
            new { authorizationId, now }, transaction, cancellationToken: cancellationToken));
    }
}
