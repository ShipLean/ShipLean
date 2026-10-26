using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Links;

public sealed record ReportLinkStateResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("as_of")] DateTimeOffset AsOf) : McpResult(Project, OpenUrl);
