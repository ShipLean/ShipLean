using System.Text.Json;
using ModelContextProtocol.Protocol;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.Asks;

// A decisions-and-questions tool result on the wire, in the McpToolResult shape. Markdown is the optional readable body.
internal static class AsksReply
{
    public static CallToolResult Of(McpResult result, string summary, string? markdown = null)
        => McpToolResult.Of(JsonSerializer.SerializeToElement(result, result.GetType()), summary, markdown);
}
