using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Agents.Stores;

public sealed class AgentClientInfo(NpgsqlDataSource dataSource) : IAgentClientInfo
{
    public const int MaxLength = 200;

    public async Task RememberAsync(Guid authorizationId, string? name, string? version, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE oauth_authorization SET mcp_client_name = @name, mcp_client_version = @version WHERE id = @authorizationId",
            new { authorizationId, name = Cut(name), version = Cut(version) }, cancellationToken: cancellationToken));
    }

    private static string? Cut(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength];
    }
}
