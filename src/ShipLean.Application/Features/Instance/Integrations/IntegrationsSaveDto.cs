namespace ShipLean.Application.Features.Instance.Integrations;

// The body of PUT api/instance/integrations. A null token or URL leaves the saved one alone; the Clear flags remove it
// (GitLabUrl "" clears the saved URL, which brings the environment's pair back). PreviewAllowHosts is the whole list.
// YouTubeClientId "" clears the saved YouTube client; a new id needs its secret, and a null secret keeps the saved one.
public sealed record IntegrationsSaveDto(
    string? GitHubToken,
    bool ClearGitHubToken,
    string? GitLabUrl,
    string? GitLabToken,
    bool ClearGitLabToken,
    IReadOnlyList<string>? PreviewAllowHosts,
    string? YouTubeClientId = null,
    string? YouTubeClientSecret = null);
