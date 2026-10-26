using Dapper;
using ShipLean.Core;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Agents.Stores;

// oauth_authorization: one live row per person and client (a unique index), carrying the user's security stamp at consent.
public sealed class AgentAuthorizations(NpgsqlDataSource dataSource, TimeProvider clock) : IAgentAuthorizations
{
    private sealed record Live(
        Guid Id,
        Array Scopes,
        string SecurityStamp)
    {
    }

    private sealed record Listed(
        Guid Id,
        string ClientName,
        string? ClientHost,
        string Kind,
        Array Scopes,
        DateTimeOffset ConnectedAt,
        DateTimeOffset? LastUsedAt)
    {
    }

    public async Task<Guid> CreateAsync(Guid userId, string clientId, IReadOnlySet<string> scopes, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // A concurrent consent for the same user and client can win the insert below; read again, and it is then the live row.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var stamp = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                "SELECT security_stamp FROM app_user WHERE id = @userId", new { userId }, transaction, cancellationToken: cancellationToken))
                ?? throw ApiException.NotFound("No such user.");
            var clientKnown = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM oauth_client WHERE client_id = @clientId AND archived_at IS NULL)",
                new { clientId }, transaction, cancellationToken: cancellationToken));
            if (!clientKnown)
            {
                throw ApiException.NotFound("No such OAuth client.");
            }

            var now = clock.GetUtcNow();
            var live = await connection.QuerySingleOrDefaultAsync<Live>(new CommandDefinition(
                """
                SELECT id, scopes, security_stamp AS securityStamp FROM oauth_authorization
                WHERE user_id = @userId AND client_id = @clientId AND revoked_at IS NULL
                FOR UPDATE
                """,
                new { userId, clientId }, transaction, cancellationToken: cancellationToken));
            if (live is not null && StoreSecrets.Same(live.SecurityStamp, stamp))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE oauth_authorization SET scopes = @scopes WHERE id = @id",
                    new { id = live.Id, scopes = ScopeLists.Sorted([.. (string[])live.Scopes, .. scopes]) }, transaction, cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
                return live.Id;
            }
            if (live is not null)
            {
                // Granted under an earlier password: end it and its tokens rather than revive them.
                await AuthorizationRevocation.RevokeAsync(connection, transaction, live.Id, now, cancellationToken);
            }

            var id = Guid.NewGuid();
            var inserted = await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO oauth_authorization (id, user_id, client_id, scopes, security_stamp, created_at)
                VALUES (@id, @userId, @clientId, @scopes, @stamp, @now)
                ON CONFLICT (user_id, client_id) WHERE revoked_at IS NULL DO NOTHING
                """,
                new { id, userId, clientId, scopes = ScopeLists.Sorted(scopes.ToArray()), stamp, now }, transaction, cancellationToken: cancellationToken));
            if (inserted == 1)
            {
                await transaction.CommitAsync(cancellationToken);
                return id;
            }
            await transaction.RollbackAsync(cancellationToken);
        }
        throw ApiException.Conflict("The agent was connected at the same moment. Try again.");
    }

    public async Task<IReadOnlyList<ConnectedAgentDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Listed>(new CommandDefinition(
            """
            SELECT a.id, c.client_name AS clientName, c.client_host AS clientHost, c.kind, a.scopes,
                   a.created_at AS connectedAt, a.last_used_at AS lastUsedAt
            FROM oauth_authorization a
            JOIN oauth_client c ON c.client_id = a.client_id
            WHERE a.user_id = @userId AND a.revoked_at IS NULL
            ORDER BY a.created_at DESC, a.id
            """,
            new { userId }, cancellationToken: cancellationToken));
        return rows
            .Select(r => new ConnectedAgentDto(
                r.Id, r.ClientName, r.ClientHost, r.Kind == "metadataDocument" && r.ClientHost is not null, (string[])r.Scopes, r.ConnectedAt, r.LastUsedAt,
                DisplayDate.Relative(r.ConnectedAt, clock), r.LastUsedAt is { } used ? DisplayDate.Relative(used, clock) : null))
            .ToList();
    }

    public async Task RevokeAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var owned = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM oauth_authorization WHERE id = @id AND user_id = @userId)",
            new { id, userId }, transaction, cancellationToken: cancellationToken));
        if (!owned)
        {
            throw ApiException.NotFound("No such connected agent.");
        }
        await AuthorizationRevocation.RevokeAsync(connection, transaction, id, clock.GetUtcNow(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
