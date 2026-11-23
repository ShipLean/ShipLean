using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// prompts without `prompt`: every built-in prompt of the project. CanEdit says whether the caller is an admin (can save).
internal sealed record PromptsListResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("prompts")] IReadOnlyList<PromptLine> Prompts,
    [property: JsonPropertyName("can_edit")] bool CanEdit,
    [property: JsonPropertyName("custom_count")] int CustomCount) : McpResult(Project, OpenUrl);
