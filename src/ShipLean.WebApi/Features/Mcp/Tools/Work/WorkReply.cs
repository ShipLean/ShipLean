using System.Text.Json;
using ModelContextProtocol.Protocol;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.Work;

// The McpResult convention, in the McpToolResult shape: the summary line and the result as JSON.
internal static class WorkReply
{
    public static CallToolResult Of(McpResult result, string summary)
        => McpToolResult.Of(JsonSerializer.SerializeToElement(result, result.GetType()), summary);
}
