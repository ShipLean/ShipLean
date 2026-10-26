namespace ShipLean.Application.Features.Instance.Integrations;

// The body of PUT api/instance/integrations. A null token or URL leaves the saved one alone; the Clear flags remove it
// (GitLabUrl "" clears the saved URL, which brings the environment's pair back). PreviewAllowHosts is the whole list.
public sealed record IntegrationsSaveDto(
    string? GitHubToken,
    bool ClearGitHubToken,
    string? GitLabUrl,
    string? GitLabToken,
    bool ClearGitLabToken,
    IReadOnlyList<string>? PreviewAllowHosts);
