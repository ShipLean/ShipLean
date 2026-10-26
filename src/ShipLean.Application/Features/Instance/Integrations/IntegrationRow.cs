namespace ShipLean.Application.Features.Instance.Integrations;

// The integration columns of the install's one instance_settings row. The allow-list is read joined into one text,
// because Dapper can't fill a collection property from a text[] column.
public sealed class IntegrationRow
{
    public const string Select =
        "SELECT github_token AS GitHubToken, github_token_last4 AS GitHubTokenLast4, gitlab_url AS GitLabUrl, gitlab_token AS GitLabToken, "
        + "gitlab_token_last4 AS GitLabTokenLast4, array_to_string(preview_allow_hosts, ',') AS PreviewAllowHostsCsv FROM instance_settings";

    public string? GitHubToken { get; set; }

    public string? GitHubTokenLast4 { get; set; }

    public string? GitLabUrl { get; set; }

    public string? GitLabToken { get; set; }

    public string? GitLabTokenLast4 { get; set; }

    public string PreviewAllowHostsCsv { get; set; } = string.Empty;

    // Host names hold no comma, so a comma-joined text survives the round trip.
    public IReadOnlyList<string> PreviewAllowHosts => PreviewAllowHostsCsv.Length == 0 ? [] : PreviewAllowHostsCsv.Split(',');
}
