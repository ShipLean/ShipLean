using System.Text.Json;
using ModelContextProtocol.Protocol;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Status;

// A tool result on the wire: the result as structured content and one line of text for the agent to read aloud.
internal static class StatusReply
{
    public static CallToolResult Of<T>(T result, string summary) where T : McpResult => new()
    {
        Content = [new TextContentBlock { Text = summary }],
        StructuredContent = JsonSerializer.SerializeToElement(result),
    };
}
