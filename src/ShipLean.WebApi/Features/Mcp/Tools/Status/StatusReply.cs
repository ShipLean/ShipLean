using System.Text.Json;
using ModelContextProtocol.Protocol;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.Status;

// A tool result on the wire, in the McpToolResult shape: the summary line and the result as JSON.
internal static class StatusReply
{
    public static CallToolResult Of<T>(T result, string summary) where T : McpResult
        => McpToolResult.Of(JsonSerializer.SerializeToElement(result), summary);
}
