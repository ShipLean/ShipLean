using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Code;

public sealed record AddedCommit(
    [property: JsonPropertyName("repo")] string Repo,
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("subject")] string Subject);
