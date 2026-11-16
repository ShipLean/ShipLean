using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// One commit a report_task call names.
public sealed record ReportCommit
{
    [JsonPropertyName("repo"), Description("The repository the commit is in, as git remote get-url origin prints it with its host, such as github.com/acme/storefront. Leave it out and give url instead.")]
    public string? Repo { get; init; }

    [JsonPropertyName("sha"), Description("The commit's full sha. A short sha of at least 7 characters is accepted with url.")]
    public string? Sha { get; init; }

    [JsonPropertyName("subject"), Description("The commit's subject line.")]
    public string? Subject { get; init; }

    [JsonPropertyName("committed_at"), Description("When the commit was authored, ISO 8601 with an offset, from git log -1 --format=%aI <sha>, such as 2026-11-16T09:30:00+02:00. Stored as UTC on the commit's link, once: a time already stored is never changed. Not later than one day from now.")]
    public string? CommittedAt { get; init; }

    [JsonPropertyName("url"), Description("The commit's web URL. Needed only when repo is left out.")]
    public string? Url { get; init; }
}
