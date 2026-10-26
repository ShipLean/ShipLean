using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

// The McpResult convention for the Orient tools: the result as structured content and one line of text. Null fields are
// left out, as the SDK's own structured content does, so a project with no sprint running has no current_sprint.
internal static class OrientReply
{
    public static CallToolResult Of<T>(T result, string summary) where T : McpResult => new()
    {
        Content = [new TextContentBlock { Text = summary }],
        StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions),
    };
}
