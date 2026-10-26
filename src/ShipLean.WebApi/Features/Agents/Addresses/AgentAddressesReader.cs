using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Features.Agents.Addresses;

// Read on every call, never cached: changing base_url disconnects agents. Outside a request there is no origin to fall back
// on, so a blank base_url there is an error rather than a guess.
public sealed class AgentAddressesReader(NpgsqlDataSource dataSource, Func<HttpContext?> currentRequest) : IAgentAddresses
{
    public async Task<AgentAddresses> ForCurrentRequestAsync(CancellationToken cancellationToken = default)
    {
        var request = currentRequest()?.Request
            ?? throw new InvalidOperationException("Agent addresses are only known inside a request.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var baseUrl = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT base_url FROM instance_settings", cancellationToken: cancellationToken));
        var requestOrigin = $"{request.Scheme}://{request.Host.ToUriComponent()}";
        return new AgentAddresses(AgentUrls.Issuer(baseUrl, requestOrigin), AgentUrls.Resource(baseUrl, requestOrigin));
    }
}
