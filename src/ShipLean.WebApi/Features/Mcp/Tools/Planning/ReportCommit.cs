using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// One commit a report_task call names.
public sealed record ReportCommit
{
    [JsonPropertyName("sha"), Description("The commit's sha, full or short.")]
    public string? Sha { get; init; }

    [JsonPropertyName("subject"), Description("The commit's subject line.")]
    public string? Subject { get; init; }

    [JsonPropertyName("url"), Description("The commit's web URL.")]
    public string? Url { get; init; }
}
