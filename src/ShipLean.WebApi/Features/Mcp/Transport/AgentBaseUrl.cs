using Agoda.IoC.Core;
using Dapper;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// Reads instance_settings.base_url for a request and hands it to AgentUrls, the one rule for the issuer and the audience of
// every token. Read on every call, never cached: changing base_url disconnects agents. When base_url is blank the origin
// is the host the request came in on (RequestOrigin), not the service DNS the last hop dialed. Middleware and policies
// pass the request the framework handed them; tools and services, which have none, read the current one (ICurrentRequest).
[RegisterSingleton]
public sealed class AgentBaseUrl(NpgsqlDataSource dataSource, IOptions<ForwardedHeadersOptions> forwarding, PublicOrigin origins, ICurrentRequest current)
{
    public Task<AgentAddresses> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
        => ReadAsync(
            RequestOrigin.Of(request, forwarding.Value),
            () => RequestOrigin.ClusterFallback(request, forwarding.Value),
            cancellationToken);

    // For the current request.
    public Task<AgentAddresses> ReadAsync(CancellationToken cancellationToken)
        => ReadAsync(current.Origin, () => current.OriginClusterFallback, cancellationToken);

    private async Task<AgentAddresses> ReadAsync(string requestOrigin, Func<string?> clusterFallback, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var baseUrl = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT base_url FROM instance_settings", cancellationToken: cancellationToken));
        var blank = string.IsNullOrWhiteSpace(baseUrl);
        origins.Note(blank, blank ? clusterFallback() : null);
        return new AgentAddresses(AgentUrls.Issuer(baseUrl, requestOrigin), AgentUrls.Resource(baseUrl, requestOrigin), !blank);
    }
}
