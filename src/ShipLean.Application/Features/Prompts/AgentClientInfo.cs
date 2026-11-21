using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Prompts;

public sealed class AgentClientInfo(NpgsqlDataSource dataSource) : IAgentClientInfo
{
    public const int MaxLength = 200;

    public async Task RememberAsync(Guid authorizationId, string? name, string? version, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE oauth_authorization SET mcp_client_name = @name, mcp_client_version = @version WHERE id = @authorizationId AND (mcp_client_name IS DISTINCT FROM @name OR mcp_client_version IS DISTINCT FROM @version)",
            new { authorizationId, name = Cut(name), version = Cut(version) }, cancellationToken: cancellationToken));
    }

    private static string? Cut(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (trimmed.Length <= MaxLength)
        {
            return trimmed;
        }
        // Never split a surrogate pair.
        var end = char.IsHighSurrogate(trimmed[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
        return trimmed[..end];
    }
}
