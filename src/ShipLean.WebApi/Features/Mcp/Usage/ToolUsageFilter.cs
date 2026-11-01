using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ShipLean.WebApi.Features.Mcp.Usage;

// Sits just inside ToolMetricsFilter. TODO: track the tool call as a usage event (its catalogue name, outcome, duration and project); for now it passes the call through.
public static class ToolUsageFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => next;
}
