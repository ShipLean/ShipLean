using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Asks;

public sealed record AnswerQuestionResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("comment_id")] Guid CommentId) : McpResult(Project, OpenUrl);
