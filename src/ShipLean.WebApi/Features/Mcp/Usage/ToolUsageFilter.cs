using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Features.Mcp.Payloads;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Usage;

// Sits just inside ToolMetricsFilter, so it sees the error result ToolErrorFilter built. Each call of a mapped tool emits one
// usage event: the tool's catalogue name, the agent as actor, the outcome and error code, the duration, and the one project
// the call touched. An unmatched or unmapped tool emits nothing. Tracking never throws into the call.
public static class ToolUsageFilter
{
    private const string UnknownErrorCode = "internal_error";

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (request, cancellationToken) =>
        {
            var started = Stopwatch.GetTimestamp();
            var outcome = UsageOutcome.Error;
            string? errorCode = UnknownErrorCode;
            try
            {
                var result = await next(request, cancellationToken);
                if (result.IsError == true)
                {
                    errorCode = ErrorCodeOf(result);
                }
                else
                {
                    outcome = UsageOutcome.Ok;
                    errorCode = null;
                }
                return result;
            }
            catch (OperationCanceledException)
            {
                outcome = UsageOutcome.Cancelled;
                errorCode = null;
                throw;
            }
            finally
            {
                Track(request, outcome, errorCode, Stopwatch.GetElapsedTime(started));
            }
        };

    private static void Track(RequestContext<CallToolRequestParams> request, UsageOutcome outcome, string? errorCode, TimeSpan elapsed)
    {
        try
        {
            if (request.MatchedPrimitive is not McpServerTool matched
                || !ToolUsageMap.Events.TryGetValue(matched.ProtocolTool.Name, out var eventName)
                || request.Services is not { } services
                || request.User?.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userText
                || !Guid.TryParse(userText, out var userId))
            {
                return;
            }

            var authorization = request.User.FindFirstValue(AgentClaims.AuthorizationClaim);
            var agentId = authorization ?? $"gw-{userId}";
            var agent = services.GetRequiredService<ICurrentAgent>();
            var scope = services.GetRequiredService<IUsageScope>();
            var time = services.GetRequiredService<TimeProvider>();
            var payloadUsed = services.GetService<IHttpContextAccessor>()?.HttpContext?.Items.TryGetValue(McpPayloadTargets.UsedItem, out var used) == true
                && used is true;

            services.GetRequiredService<IUsageEvents>().Track(new UsageEvent(
                eventName,
                UsageSurface.Mcp,
                UsageActor.ForAgent(userId, agentId, agent.Name, agent.Host, agent.Verified),
                scope.ProjectId,
                scope.ProjectCount,
                outcome,
                errorCode,
                (long)elapsed.TotalMilliseconds,
                $"tool:{matched.ProtocolTool.Name}",
                time.GetUtcNow(),
                ToolUsageProps.For(McpPayloadTargets.For(matched) is not null, payloadUsed)));
        }
        catch (Exception e)
        {
            request.Services?.GetService<ILoggerFactory>()?.CreateLogger(typeof(ToolUsageFilter))
                .LogDebug(e, "Could not track the usage of tool {Tool}.", request.Params?.Name);
        }
    }

    // The code ToolErrorFilter put in the McpError result, at error.code.
    private static string ErrorCodeOf(CallToolResult result)
        => result.StructuredContent is { ValueKind: JsonValueKind.Object } body
            && body.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("code", out var code)
            && code.ValueKind == JsonValueKind.String
                ? code.GetString()!
                : UnknownErrorCode;
}
