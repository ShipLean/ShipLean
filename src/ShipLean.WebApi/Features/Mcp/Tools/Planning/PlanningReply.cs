using System.Text.Json;
using ModelContextProtocol.Protocol;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// A planning tool result on the wire, in the McpToolResult shape. Markdown is the brief or guide part the result carries.
internal static class PlanningReply
{
    public static CallToolResult Of(McpResult result, string summary, string? markdown = null)
        => McpToolResult.Of(JsonSerializer.SerializeToElement(result, result.GetType()), summary, markdown);

    // The project key of a resolved item key ("SHIP-12" is in SHIP).
    public static string ProjectKeyOf(string itemKey) => itemKey[..itemKey.IndexOf('-')];
}
