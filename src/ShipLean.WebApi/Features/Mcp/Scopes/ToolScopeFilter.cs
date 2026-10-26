using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Mcp.Errors;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Scopes;

// The backstop behind the scope gate. The gate reads the request body; this reads what the SDK actually matched (the tool)
// and bound (the arguments), so if the two ever disagree the call fails closed: the token must carry the scope that tool
// needs for those arguments, and a writing call must be one the gate counted against the per-user write limit. A mismatch
// is a forbidden McpError and is logged; the tool does not run.
public static class ToolScopeFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (request, cancellationToken) =>
        {
            if (Mismatch(request) is { } reason)
            {
                request.Services?.GetService<ILoggerFactory>()?.CreateLogger(typeof(ToolScopeFilter))
                    .LogWarning("Refused tool call {Tool}: {Reason}.", request.Params?.Name, reason);
                return ToolErrorFilter.Result(ApiException.Forbidden("This agent was not granted what that tool needs."));
            }
            return await next(request, cancellationToken);
        };

    // Why the call must not run, or null.
    private static string? Mismatch(RequestContext<CallToolRequestParams> request)
    {
        if (request.MatchedPrimitive is not McpServerTool tool)
        {
            // No tool matched: the SDK answers an unknown name itself. A name that is registered yet unmatched is a disagreement.
            return request.Server.ServerOptions.ToolCollection is { } tools && request.Params is { } named && tools.TryGetPrimitive(named.Name, out _)
                ? "the SDK matched no tool for a registered name"
                : null;
        }
        var httpContext = request.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
        if (request.User is not { } user || httpContext is null)
        {
            return "no caller";
        }
        string required;
        try
        {
            required = ToolScopes.Required(tool, request.Params?.Arguments);
        }
        catch (InvalidOperationException)
        {
            return "the tool has no scope";
        }
        if (!AgentClaims.ScopesOf(user).Contains(required))
        {
            return $"the token lacks {required}";
        }
        if (ToolScopes.IsWriting(required) && httpContext.Items[McpPipeline.WritingUserItem] is not string)
        {
            return "a writing call the gate did not count";
        }
        return null;
    }
}
