using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Instance.Integrations;

// IIntegrationSettings: a saved value first, then the environment (SHIPLEAN_GITHUB_TOKEN, SHIPLEAN_GITLAB_URL,
// SHIPLEAN_GITLAB_TOKEN). The GitLab token is read from the place the GitLab URL is read from: a saved URL pairs only
// with a saved token and the environment's URL only with the environment's token, so a token never reaches a host
// its owner did not pair it with. GitHubApiUrl is environment-only.
public sealed class DbIntegrationSettings(NpgsqlDataSource dataSource, IntegrationTokens tokens, IConfiguration configuration) : IIntegrationSettings
{
    public async Task<IntegrationSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // No row (an install not yet set up) reads as nothing saved.
        var row = await connection.QuerySingleOrDefaultAsync<IntegrationRow>(new CommandDefinition(IntegrationRow.Select, cancellationToken: cancellationToken)) ?? new IntegrationRow();

        var githubToken = row.GitHubToken is { } savedGitHub ? tokens.Unprotect(savedGitHub) : IntegrationEnv.Read(configuration, "SHIPLEAN_GITHUB_TOKEN");
        string? gitlabUrl;
        string? gitlabToken;
        if (row.GitLabUrl is { } savedUrl)
        {
            gitlabUrl = savedUrl;
            gitlabToken = row.GitLabToken is { } savedGitLab ? tokens.Unprotect(savedGitLab) : null;
        }
        else
        {
            // The environment's token belongs to the environment's URL: with no URL there is nothing to pair it with.
            gitlabUrl = IntegrationEnv.Read(configuration, "SHIPLEAN_GITLAB_URL");
            gitlabToken = gitlabUrl is null ? null : IntegrationEnv.Read(configuration, "SHIPLEAN_GITLAB_TOKEN");
        }
        return new IntegrationSettingsDto(githubToken, IntegrationEnv.GitHubApiUrl(configuration), gitlabUrl, gitlabToken, row.PreviewAllowHosts);
    }
}
