using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.WebApi.Infrastructure.Telemetry;

namespace ShipLean.WebApi.Features.Mcp.Errors;

// Outermost on a tool call, so it sees the final result: an error result or a throw that gets past the error filter is "error",
// cancellation is "cancelled". The tool tag is the matched tool's own name, never the client's string, which keeps it bounded.
public static class ToolMetricsFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (request, cancellationToken) =>
        {
            var tool = request.MatchedPrimitive is McpServerTool matched ? matched.ProtocolTool.Name : "unknown";
            var started = Stopwatch.GetTimestamp();
            var outcome = "error";
            try
            {
                var result = await next(request, cancellationToken);
                outcome = result.IsError == true ? "error" : "ok";
                return result;
            }
            catch (OperationCanceledException)
            {
                outcome = "cancelled";
                throw;
            }
            finally
            {
                request.Services?.GetService<ShipLeanMetrics>()?.ToolCalled(tool, outcome, Stopwatch.GetElapsedTime(started));
            }
        };
}
