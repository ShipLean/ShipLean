using System.Text.Json;
using ModelContextProtocol.Protocol;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Work;

// The McpResult convention: the result is the structured content, with one line of text for a client that shows only text.
internal static class WorkReply
{
    public static CallToolResult Of(McpResult result, string summary) => new()
    {
        Content = [new TextContentBlock { Text = summary }],
        StructuredContent = JsonSerializer.SerializeToElement(result, result.GetType()),
    };
}
