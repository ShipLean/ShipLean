using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ShipLean.WebApi.Features.Mcp.Payloads;

// Expands a call's `payload` argument into the arguments the tool binds. It sits inside ToolErrorFilter (its refusals become
// tool errors) and outside ToolScopeFilter (the scope backstop sees the expanded arguments).
public static class PayloadFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        // TODO: expand the payload argument into the call's arguments (find it bound to the caller, inline arguments win, refuse plan and payload inside it).
        => (request, cancellationToken) => next(request, cancellationToken);
}
