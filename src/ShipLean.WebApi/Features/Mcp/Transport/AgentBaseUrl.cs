using Agoda.IoC.Core;
using Dapper;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Npgsql;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// Reads instance_settings.base_url for a request and hands it to AgentUrls, the one rule for the issuer and the audience of
// every token. Read on every call, never cached: changing base_url disconnects agents. When base_url is blank the origin
// is the host the request came in on (RequestOrigin), not the service DNS the last hop dialed.
[RegisterSingleton]
public sealed class AgentBaseUrl(NpgsqlDataSource dataSource, IOptions<ForwardedHeadersOptions> forwarding)
{
    public async Task<AgentAddresses> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var baseUrl = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT base_url FROM instance_settings", cancellationToken: cancellationToken));
        var requestOrigin = RequestOrigin.Of(request, forwarding.Value);
        return new AgentAddresses(AgentUrls.Issuer(baseUrl, requestOrigin), AgentUrls.Resource(baseUrl, requestOrigin), !string.IsNullOrWhiteSpace(baseUrl));
    }
}
