using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Stores;

// transfer_token: 32 random bytes, base64url, stored as SHA-256 only. A token dies when it expires, is spent (an upload is spent
// once its upload is claimed, and given back if the upload fails; a download is never spent), when the agent authorization that asked for it is revoked, or when
// its user is disabled or their security stamp no longer matches the authorization's.
public sealed class TransferTokens(NpgsqlDataSource dataSource, TimeProvider clock) : ITransferTokens
{
    private sealed record Row(Guid WorkItemId, Guid? AttachmentId, string Name, string ContentType, Guid UserId, string AgentStamp, DateTimeOffset ExpiresAt);

    public async Task<IssuedTransfer> IssueAsync(NewTransfer transfer, CancellationToken cancellationToken = default)
    {
        var token = StoreSecrets.New();
        var expiresAt = clock.GetUtcNow().Add(ITransferTokens.Lifetime);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO transfer_token (token_hash, kind, work_item_id, attachment_id, name, content_type, user_id, authorization_id, agent_stamp, expires_at)
            VALUES (@hash, @kind, @workItemId, @attachmentId, @name, @contentType, @userId, @authorizationId, @agentStamp, @expiresAt)
            """,
            new
            {
                hash = StoreSecrets.Hash(token), kind = Wire(transfer.Kind), transfer.WorkItemId, transfer.AttachmentId, transfer.Name, transfer.ContentType,
                transfer.UserId, transfer.AuthorizationId, transfer.AgentStamp, expiresAt,
            },
            cancellationToken: cancellationToken));
        return new IssuedTransfer(token, expiresAt);
    }

    public async Task<TransferGrant?> FindAsync(TransferKind kind, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            """
            SELECT t.work_item_id AS workItemId, t.attachment_id AS attachmentId, t.name, t.content_type AS contentType,
                   t.user_id AS userId, t.agent_stamp AS agentStamp, t.expires_at AS expiresAt
            FROM transfer_token t
            JOIN app_user u ON u.id = t.user_id AND u.disabled_at IS NULL
            LEFT JOIN oauth_authorization a ON a.id = t.authorization_id
            WHERE t.token_hash = @hash AND t.kind = @kind AND t.used_at IS NULL AND t.expires_at > @now
              AND (t.authorization_id IS NULL OR (a.revoked_at IS NULL AND a.security_stamp = u.security_stamp))
            """,
            new { hash = StoreSecrets.Hash(token), kind = Wire(kind), now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
        return row is null ? null : new TransferGrant(kind, row.WorkItemId, row.AttachmentId, row.Name, row.ContentType, row.UserId, row.AgentStamp, row.ExpiresAt);
    }

    public async Task<bool> ClaimAsync(string token, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var claimed = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE transfer_token SET used_at = @now WHERE token_hash = @hash AND kind = 'upload' AND used_at IS NULL AND expires_at > @now",
            new { hash = StoreSecrets.Hash(token), now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
        return claimed == 1;
    }

    public async Task ReleaseAsync(string token, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE transfer_token SET used_at = NULL WHERE token_hash = @hash AND kind = 'upload'",
            new { hash = StoreSecrets.Hash(token) }, cancellationToken: cancellationToken));
    }

    private static string Wire(TransferKind kind) => kind == TransferKind.Upload ? "upload" : "download";
}
