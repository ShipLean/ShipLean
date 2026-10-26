using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// A commit linked to a task, read back from its link.
public sealed record PlanCommit(
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("url")] string Url);
