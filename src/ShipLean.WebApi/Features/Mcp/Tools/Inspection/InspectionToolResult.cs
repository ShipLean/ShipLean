using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Inspection;

// The K5 view a tool returns, under "data", with the usual project and open_url.
public sealed record InspectionToolResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("data")] object Data) : McpResult(Project, OpenUrl);
