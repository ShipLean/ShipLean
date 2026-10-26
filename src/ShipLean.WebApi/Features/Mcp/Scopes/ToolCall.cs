using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace ShipLean.WebApi.Features.Mcp.Scopes;

// The tool a tools/call message names and its arguments (so the gate can tell whether a non-null `plan` came with it), read from the message the SDK
// itself parsed (its types, its options) so the gate and the SDK can never read one body two ways.
public sealed record ToolCall(string Tool, IDictionary<string, JsonElement>? Arguments)
{
    public const string Method = "tools/call";

    // Null when the message is not a tools/call. Throws JsonException when it is one the SDK could not bind.
    public static ToolCall? From(JsonRpcMessage message)
    {
        var parameters = message switch
        {
            JsonRpcRequest { Method: Method } request => request.Params,
            JsonRpcNotification { Method: Method } notification => notification.Params,
            _ => null,
        };
        if (message is not (JsonRpcRequest { Method: Method } or JsonRpcNotification { Method: Method }))
        {
            return null;
        }
        var call = parameters?.Deserialize<CallToolRequestParams>(McpJsonUtilities.DefaultOptions)
                   ?? throw new JsonException("tools/call has no params.");
        return new ToolCall(call.Name, call.Arguments);
    }

    public bool PlanGiven => PlanGivenIn(Arguments);

    public static bool PlanGivenIn(IDictionary<string, JsonElement>? arguments)
        => arguments is not null
           && arguments.TryGetValue("plan", out var plan)
           && plan.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
}
