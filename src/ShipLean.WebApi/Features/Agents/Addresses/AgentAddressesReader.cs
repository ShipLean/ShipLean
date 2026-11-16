using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Features.Agents.Addresses;

// Read on every call, never cached: changing base_url disconnects agents. Outside a request there is no origin to fall back
// on, so a blank base_url there is an error rather than a guess. When base_url is blank the origin is the host the
// request came in on (ICurrentRequest.Origin), not the service DNS the last hop dialed.
public sealed class AgentAddressesReader(NpgsqlDataSource dataSource, ICurrentRequest request) : IAgentAddresses
{
    public async Task<AgentAddresses> ForCurrentRequestAsync(CancellationToken cancellationToken = default)
    {
        if (!request.Exists)
        {
            throw new InvalidOperationException("Agent addresses are only known inside a request.");
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var baseUrl = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT base_url FROM instance_settings", cancellationToken: cancellationToken));
        var requestOrigin = request.Origin;
        return new AgentAddresses(AgentUrls.Issuer(baseUrl, requestOrigin), AgentUrls.Resource(baseUrl, requestOrigin));
    }
}
