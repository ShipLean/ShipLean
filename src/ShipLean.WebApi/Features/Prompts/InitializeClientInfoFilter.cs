using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Agents.Stores;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Prompts;

// The transport is stateless, so no MCP session remembers who connected: the handshake stores the client's name and version on
// the token's authorization, and every later call through any token of it reads that (DropMCP caches it per session instead).
// The handshake is `initialize` (params.clientInfo) or, from clients on the newer stateless protocol, `server/discover` (the
// clientInfo in params._meta). Telemetry: a failed store is logged and the handshake carries on.
public static class InitializeClientInfoFilter
{
    private const string MetaClientInfo = "io.modelcontextprotocol/clientInfo";

    public static McpMessageHandler Apply(McpMessageHandler next)
        => async (context, cancellationToken) =>
        {
            if (context.JsonRpcMessage is JsonRpcRequest { Method: "initialize" or "server/discover" } request
                && context.User?.FindFirst(AgentClaims.AuthorizationClaim)?.Value is { } id && Guid.TryParse(id, out var authorizationId))
            {
                await RememberAsync(context, request, authorizationId, cancellationToken);
            }
            await next(context, cancellationToken);
        };

    private static async Task RememberAsync(MessageContext context, JsonRpcRequest request, Guid authorizationId, CancellationToken cancellationToken)
    {
        if (context.Services is not { } services)
        {
            return;
        }
        try
        {
            var parameters = request.Params as JsonObject;
            var info = (parameters?["clientInfo"] ?? (parameters?["_meta"] as JsonObject)?[MetaClientInfo]) as JsonObject;
            if (info?["name"] is JsonValue name && name.TryGetValue<string>(out var clientName))
            {
                var version = info["version"] is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
                await services.GetRequiredService<IAgentClientInfo>().RememberAsync(authorizationId, clientName, version, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetService<ILoggerFactory>()?.CreateLogger(typeof(InitializeClientInfoFilter))
                .LogWarning(exception, "Could not remember the MCP client name sent in the handshake.");
        }
    }
}
