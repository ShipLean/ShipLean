using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// applied false: a preview, nothing written (BasedOn, NextVersion, Patch, Bytes, Plan and ExpiresInMinutes are set; pass `plan` back to apply).
// applied true: the version saved (Version, Status). Warnings are on both: checks that don't stop the save.
internal sealed record SavePromptResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("based_on")] string? BasedOn,
    [property: JsonPropertyName("next_version")] int? NextVersion,
    [property: JsonPropertyName("patch")] string? Patch,
    [property: JsonPropertyName("bytes")] int? Bytes,
    [property: JsonPropertyName("version")] int? Version,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("warnings")] IReadOnlyList<PromptWarningLine> Warnings,
    [property: JsonPropertyName("plan")] string? Plan,
    [property: JsonPropertyName("expires_in_minutes")] int? ExpiresInMinutes) : McpResult(Project, OpenUrl);
