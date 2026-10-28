using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Instance.AgentConnections;

// IAgentConnections over agent_connection. Only GetAsync decrypts the signing secret.
public sealed class AgentConnections(NpgsqlDataSource dataSource, AgentConnectionSecrets secrets) : IAgentConnections
{
    public async Task<IReadOnlyList<AgentConnectionDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AgentConnectionRow>(new CommandDefinition(
            $"SELECT {AgentConnectionRow.Columns} FROM agent_connection ORDER BY archived_at IS NOT NULL, lower(name) COLLATE \"C\", id", cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToDto())];
    }

    public async Task<AgentConnection?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AgentConnectionRow>(new CommandDefinition(
            $"SELECT {AgentConnectionRow.Columns} FROM agent_connection WHERE id = @id", new { id }, cancellationToken: cancellationToken));
        return row is null ? null : With(row);
    }

    public async Task<AgentConnectionIdentity?> FindActiveByCallbackTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AgentConnectionIdentity>(new CommandDefinition(
            $"SELECT id, name, type FROM agent_connection WHERE callback_token_hash = @tokenHash AND archived_at IS NULL LIMIT 1",
            new { tokenHash }, cancellationToken: cancellationToken));
        return row;
    }

    private AgentConnection With(AgentConnectionRow row)
        => new(row.Id, row.Type, row.Name, row.ReceiverUrl, secrets.Unprotect(row.SigningSecretProtected), row.ArchivedAt);
}
