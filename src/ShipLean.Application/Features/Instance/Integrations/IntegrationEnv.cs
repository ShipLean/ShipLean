using Microsoft.Extensions.Configuration;

namespace ShipLean.Application.Features.Instance.Integrations;

// The SHIPLEAN_* variables the integrations fall back to. Blank counts as unset.
public static class IntegrationEnv
{
    public const string DefaultGitHubApiHost = "api.github.com";

    public static string? Read(IConfiguration configuration, string key) => string.IsNullOrWhiteSpace(configuration[key]) ? null : configuration[key]!.Trim();

    public static string GitHubApiUrl(IConfiguration configuration) => Read(configuration, "SHIPLEAN_GITHUB_API_URL") ?? $"https://{DefaultGitHubApiHost}";
}
