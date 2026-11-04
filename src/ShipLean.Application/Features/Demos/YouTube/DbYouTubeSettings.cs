using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ShipLean.Application.Integrations;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

// IYouTubeSettings: the client saved in Instance settings first, then SHIPLEAN_YOUTUBE_CLIENT_ID and _SECRET, read when
// asked. Neither means YouTube is off. The secret is decrypted here and goes only into the returned object.
public sealed class DbYouTubeSettings(NpgsqlDataSource dataSource, IntegrationTokens tokens, IConfiguration configuration) : IYouTubeSettings
{
    public async Task<YouTubeClientSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<IntegrationRow>(new CommandDefinition(IntegrationRow.Select, cancellationToken: cancellationToken));
        if (row is { YouTubeClientId: { } id, YouTubeClientSecret: { } secret })
        {
            return new YouTubeClientSettings(id, tokens.Unprotect(secret));
        }
        return YouTubeClientEnv.Read(configuration);
    }
}
