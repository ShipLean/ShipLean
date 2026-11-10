using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Asks;

public sealed record AnswerDecisionResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("current_answer")] string CurrentAnswer) : McpResult(Project, OpenUrl);
