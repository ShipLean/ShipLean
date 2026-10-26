using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Core.Telemetry;

namespace ShipLean.WebApi.Features.Mcp.Errors;

// One span per tool call, outside the error filter so it sees the outcome after an exception became an McpError. Tags: the
// tool, the project key the caller named (when it did), the agent's client name (when an agent calls) and the outcome with
// the error code. Never the arguments or the result: they carry titles and comments.
public static class ToolTraceFilter
{
    private const int MaxTagLength = 64;

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (request, cancellationToken) =>
        {
            using var activity = ShipLeanActivities.Source.StartActivity("mcp.tool_call");
            if (activity is null)
            {
                return await next(request, cancellationToken);
            }

            activity.SetTag("mcp.tool", request.Params?.Name);
            if (request.Params?.Arguments is { } arguments
                && arguments.TryGetValue("project", out var project)
                && project.ValueKind is JsonValueKind.String)
            {
                activity.SetTag("shiplean.project", Truncate(project.GetString()));
            }

            if (request.Services?.GetService<ICurrentAgent>()?.Name is { } agentName)
            {
                activity.SetTag("shiplean.agent_name", Truncate(agentName));
            }

            try
            {
                var result = await next(request, cancellationToken);
                Record(activity, result);
                return result;
            }
            catch (Exception e)
            {
                // Cancellation and protocol errors pass through the error filter; the span still says how it ended.
                activity.SetTag("shiplean.outcome", e is OperationCanceledException ? "cancelled" : "error");
                activity.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        };

    private static void Record(Activity activity, CallToolResult result)
    {
        if (result.IsError != true)
        {
            activity.SetTag("shiplean.outcome", "ok");
            return;
        }

        activity.SetTag("shiplean.outcome", "error");
        activity.SetStatus(ActivityStatusCode.Error);
        if (result.StructuredContent is { } content
            && content.ValueKind is JsonValueKind.Object
            && content.TryGetProperty("error", out var error)
            && error.ValueKind is JsonValueKind.Object
            && error.TryGetProperty("code", out var code)
            && code.ValueKind is JsonValueKind.String)
        {
            activity.SetTag("shiplean.error_code", code.GetString());
        }
    }

    private static string? Truncate(string? value) => value is { Length: > MaxTagLength } ? value[..MaxTagLength] : value;
}
