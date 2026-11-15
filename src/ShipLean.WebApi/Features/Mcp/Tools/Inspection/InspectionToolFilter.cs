using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Inspection;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Mcp.Errors;

namespace ShipLean.WebApi.Features.Mcp.Tools.Inspection;

// While SHIPLEAN_INSPECTION is off the three Inspection tools are not listed and a direct call is a not_found tool error (K6).
public static class InspectionToolFilter
{
    public static readonly IReadOnlySet<string> ToolNames = new HashSet<string>(StringComparer.Ordinal) { "list_runs", "get_run", "agent_setup" };

    public static McpRequestHandler<ListToolsRequestParams, ListToolsResult> ApplyToList(McpRequestHandler<ListToolsRequestParams, ListToolsResult> next)
        => async (request, cancellationToken) =>
        {
            var result = await next(request, cancellationToken);
            if (!Enabled(request.Services))
            {
                result.Tools = [.. result.Tools.Where(tool => !ToolNames.Contains(tool.Name))];
            }
            return result;
        };

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> ApplyToCall(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => (request, cancellationToken) => request.Params is { Name: { } name } && ToolNames.Contains(name) && !Enabled(request.Services)
            ? ValueTask.FromResult(ToolErrorFilter.Result(ApiException.NotFound("Inspection is off.")))
            : next(request, cancellationToken);

    private static bool Enabled(IServiceProvider? services) => services?.GetService(typeof(InspectionOptions)) is InspectionOptions { Enabled: true };
}
