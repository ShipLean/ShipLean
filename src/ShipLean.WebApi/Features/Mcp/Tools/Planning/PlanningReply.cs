using System.Text.Json;
using ModelContextProtocol.Protocol;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// A planning tool result on the wire: the result as structured content and one line of text for the agent to read aloud.
internal static class PlanningReply
{
    public static CallToolResult Of(McpResult result, string summary) => new()
    {
        Content = [new TextContentBlock { Text = summary }],
        StructuredContent = JsonSerializer.SerializeToElement(result, result.GetType()),
    };

    // The project key of a resolved item key ("SHIP-12" is in SHIP).
    public static string ProjectKeyOf(string itemKey) => itemKey[..itemKey.IndexOf('-')];
}
