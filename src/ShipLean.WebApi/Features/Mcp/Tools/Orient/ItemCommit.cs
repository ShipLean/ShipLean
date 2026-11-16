using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

// A commit on a Task or Bug: the repository's path (acme/storefront) and the full sha.
public sealed record ItemCommit(
    [property: JsonPropertyName("repo")] string Repo,
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("additions")] int Additions,
    [property: JsonPropertyName("deletions")] int Deletions);
