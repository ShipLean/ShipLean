using System.Text.Json;
using ModelContextProtocol.Protocol;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// The McpResult convention: the result is the structured content, with one line of text for a client that shows only text.
internal static class SprintReply
{
    public static CallToolResult Of<T>(T result, string summary) where T : McpResult => new()
    {
        Content = [new TextContentBlock { Text = summary }],
        StructuredContent = JsonSerializer.SerializeToElement(result),
    };
}
