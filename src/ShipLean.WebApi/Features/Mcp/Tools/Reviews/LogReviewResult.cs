using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Reviews;

public sealed record LogReviewResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("verdict")] string Verdict,
    [property: JsonPropertyName("at")] string At,
    [property: JsonPropertyName("round")] int Round) : McpResult(Project, OpenUrl);
