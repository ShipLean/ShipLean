namespace ShipLean.Contracts.Links;

// The provider settings the link sources use. Tokens are secrets: they never leave the server in a response.
public sealed record IntegrationSettingsDto(string? GitHubToken, string GitHubApiUrl, string? GitLabUrl, string? GitLabToken, IReadOnlyList<string> PreviewAllowHosts);
