using System.Text.RegularExpressions;
using Agoda.IoC.Core;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Instance.Integrations;

// The Integrations tab of Instance settings, over the instance_settings row. Tokens go in encrypted and come out only as
// their last four characters; this class never decrypts one.
[RegisterPerRequest]
public sealed partial class IntegrationsSettings(NpgsqlDataSource dataSource, IntegrationTokens tokens, IConfiguration configuration, TimeProvider clock)
{
    private const int MaxHosts = 100;

    public async Task<IntegrationsSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return Present(await ReadAsync(connection, cancellationToken));
    }

    public async Task<IntegrationsSettingsDto> SaveAsync(IntegrationsSaveDto body, CancellationToken cancellationToken)
    {
        var hosts = body.PreviewAllowHosts is null ? null : NormaliseHosts(body.PreviewAllowHosts);
        var gitlabUrl = body.GitLabUrl is null ? null : NormaliseGitLabUrl(body.GitLabUrl);
        var newGitHub = body.GitHubToken is null ? null : IntegrationTokens.Require(body.GitHubToken, "gitHubToken");
        var newGitLab = body.GitLabToken is null ? null : IntegrationTokens.Require(body.GitLabToken, "gitLabToken");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await ReadAsync(connection, cancellationToken);

        var (githubToken, githubLast4) = Token(row.GitHubToken, row.GitHubTokenLast4, newGitHub, body.ClearGitHubToken);
        var url = ChosenUrl(gitlabUrl, row.GitLabUrl);
        // A token belongs to the URL it was saved with: a changed or cleared URL without a new token drops the old one.
        var urlChanged = !string.Equals(url, row.GitLabUrl, StringComparison.Ordinal);
        var (gitlabToken, gitlabLast4) = Token(urlChanged ? null : row.GitLabToken, urlChanged ? null : row.GitLabTokenLast4, newGitLab, body.ClearGitLabToken);
        if (gitlabToken is not null && url is null)
        {
            throw ApiException.Validation("Set the GitLab URL before its token: a token is kept with the URL it is for.", "gitLabToken");
        }
        if (githubToken is not null && !IsHttps(IntegrationEnv.GitHubApiUrl(configuration)))
        {
            throw ApiException.Validation("A GitHub token is only sent to an https API address. Set SHIPLEAN_GITHUB_API_URL to an https address first.", "githubApiUrl");
        }
        if (gitlabToken is not null && (body.GitLabUrl is not null || newGitLab is not null) && !IsHttps(url))
        {
            throw ApiException.Validation("A GitLab token is only sent to an https address. Use an https GitLab URL.", "gitLabUrl");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE instance_settings SET github_token = @githubToken, github_token_last4 = @githubLast4, gitlab_url = @url,
                gitlab_token = @gitlabToken, gitlab_token_last4 = @gitlabLast4, preview_allow_hosts = @hosts, updated_at = @now
            """,
            new { githubToken, githubLast4, url, gitlabToken, gitlabLast4, hosts = hosts ?? [.. row.PreviewAllowHosts], now = clock.GetUtcNow() },
            cancellationToken: cancellationToken));
        return Present(await ReadAsync(connection, cancellationToken));
    }

    private (string? Protected, string? LastFour) Token(string? saved, string? savedLast4, string? replacement, bool clear)
    {
        if (replacement is not null)
        {
            return (tokens.Protect(replacement), IntegrationTokens.LastFour(replacement));
        }
        return clear ? (null, null) : (saved, savedLast4);
    }

    private static bool IsHttps(string? url) => url is not null && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    // null leaves the saved URL, "" clears it, anything else replaces it.
    private static string? ChosenUrl(string? requested, string? saved)
    {
        if (requested is null)
        {
            return saved;
        }
        return requested.Length == 0 ? null : requested;
    }

    private IntegrationsSettingsDto Present(IntegrationRow row)
    {
        var envGitHub = IntegrationEnv.Read(configuration, "SHIPLEAN_GITHUB_TOKEN");
        var githubSaved = row.GitHubToken is not null;
        var gitlabSaved = row.GitLabUrl is not null;
        var envGitLabUrl = IntegrationEnv.Read(configuration, "SHIPLEAN_GITLAB_URL");
        var envGitLabToken = IntegrationEnv.Read(configuration, "SHIPLEAN_GITLAB_TOKEN");
        var githubLast4 = githubSaved ? row.GitHubTokenLast4 : ShownFromEnv(envGitHub);
        string? gitlabLast4;
        if (gitlabSaved)
        {
            gitlabLast4 = row.GitLabToken is null ? null : Shown(row.GitLabTokenLast4);
        }
        else
        {
            gitlabLast4 = envGitLabUrl is null ? null : ShownFromEnv(envGitLabToken);
        }
        return new IntegrationsSettingsDto(
            githubSaved ? Shown(githubLast4) : githubLast4,
            !githubSaved && envGitHub is not null,
            gitlabSaved ? row.GitLabUrl : envGitLabUrl,
            gitlabLast4,
            !gitlabSaved && envGitLabUrl is not null,
            row.PreviewAllowHosts);
    }

    private static string? ShownFromEnv(string? token) => token is null ? null : Shown(IntegrationTokens.LastFour(token));

    // A set token too short to show its end is still "set": the empty string says so without showing anything.
    private static string Shown(string? lastFour) => lastFour ?? string.Empty;

    private static Task<IntegrationRow> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
        => connection.QuerySingleAsync<IntegrationRow>(new CommandDefinition(IntegrationRow.Select, cancellationToken: cancellationToken));

    // An absolute http(s) address with no query, fragment or credentials, without a trailing "/"; "" means clear.
    private static string NormaliseGitLabUrl(string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0)
        {
            throw ApiException.Validation("The GitLab URL must be an http or https address with no query or credentials, such as https://gitlab.example.com.", "gitLabUrl");
        }
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    // One host name a line, no scheme, path or port; blank lines are ignored, repeats are dropped.
    private static string[] NormaliseHosts(IEnumerable<string> lines)
    {
        var hosts = new List<string>();
        foreach (var line in lines.Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length > 0))
        {
            if (line.Length > 253 || !HostName().IsMatch(line))
            {
                throw ApiException.Validation("Each line must be a host name only, such as example.com: no scheme, path or port.", "previewAllowHosts");
            }
            if (!hosts.Contains(line))
            {
                hosts.Add(line);
            }
        }
        if (hosts.Count > MaxHosts)
        {
            throw ApiException.Validation($"The allow-list can hold at most {MaxHosts} hosts.", "previewAllowHosts");
        }
        return [.. hosts];
    }

    [GeneratedRegex("^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)*$")]
    private static partial Regex HostName();
}
