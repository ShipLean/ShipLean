namespace ShipLean.Application.Features.Instance.Integrations;

// What GET api/instance/integrations returns. A token is never in it, only its last four characters; *FromEnv says the
// YouTube is the instance's own OAuth client, not sign-in's. The value in force comes from the environment, not from a saved setting.
public sealed record IntegrationsSettingsDto(
    string? GitHubTokenLastFour,
    bool GitHubTokenFromEnv,
    string? GitLabUrl,
    string? GitLabTokenLastFour,
    bool GitLabFromEnv,
    IReadOnlyList<string> PreviewAllowHosts,
    string? YouTubeClientId,
    string? YouTubeClientSecretLastFour,
    bool YouTubeFromEnv);
