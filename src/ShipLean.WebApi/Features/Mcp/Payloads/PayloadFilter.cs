using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Payloads;

// Expands a call's `payload` argument into the arguments the tool binds. It sits inside ToolErrorFilter (its refusals become
// tool errors) and outside ToolScopeFilter (the scope backstop sees the expanded arguments). Inline arguments win over the
// payload's; `plan` and `payload` never come from a payload, because the scope gate decides preview or apply from the inline
// `plan`. The payload's id and content never reach a log or a trace; only its size does.
public static class PayloadFilter
{
    private const string NotValid = "That payload is not valid: it expired, was never uploaded, or was made for another tool or agent. Upload it again with create_payload_upload.";

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (request, cancellationToken) =>
        {
            if (request.MatchedPrimitive is not McpServerTool tool
                || McpPayloadTargets.For(tool) is not { } target
                || request.Params is not { Arguments: { } arguments } parameters
                || !arguments.TryGetValue(McpPayloadTargets.ArgumentName, out var argument)
                || argument.ValueKind == JsonValueKind.Null
                || request.Services is not { } services)
            {
                // No payload: JSON null is an omitted optional argument, so the call passes through unexpanded.
                return await next(request, cancellationToken);
            }
            if (argument.ValueKind != JsonValueKind.String)
            {
                throw ApiException.Validation("payload must be the id create_payload_upload returned.", McpPayloadTargets.ArgumentName);
            }
            if (request.User?.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userText)
            {
                // Fail closed: a caller with no user id can own no payload, so it gets the unknown-payload refusal without a lookup.
                throw ApiException.NotFound(NotValid, McpPayloadTargets.ArgumentName);
            }

            var authorization = request.User.FindFirstValue(AgentClaims.AuthorizationClaim);
            var payload = await services.GetRequiredService<IToolPayloads>().FindAsync(
                argument.GetString()!, target.Tool, Guid.Parse(userText), authorization is null ? null : Guid.Parse(authorization), cancellationToken)
                ?? throw ApiException.NotFound(NotValid, McpPayloadTargets.ArgumentName);
            Activity.Current?.SetTag("shiplean.payload_bytes", payload.Body.Length);

            var expanded = new Dictionary<string, JsonElement>(arguments);
            expanded.Remove(McpPayloadTargets.ArgumentName);
            foreach (var (name, value) in Fields(payload, target))
            {
                expanded.TryAdd(name, value);
            }
            parameters.Arguments = expanded;
            if (services.GetService<IHttpContextAccessor>()?.HttpContext is { } httpContext)
            {
                httpContext.Items[McpPayloadTargets.UsedItem] = true;
            }
            return await next(request, cancellationToken);
        };

    // The arguments a payload gives: an object payload's top-level properties, or a field payload's text as that one argument.
    private static IReadOnlyList<KeyValuePair<string, JsonElement>> Fields(ToolPayload payload, PayloadTarget target)
    {
        if (payload.Field is { } field)
        {
            return target.TextFields.Contains(field)
                ? [new(field, JsonSerializer.SerializeToElement(Encoding.UTF8.GetString(payload.Body)))]
                : throw ApiException.Validation($"That payload fills {field}, which {target.Tool} no longer takes as text. Upload it again with create_payload_upload.", McpPayloadTargets.ArgumentName);
        }

        using var document = JsonDocument.Parse(payload.Body);
        var properties = document.RootElement.EnumerateObject().Select(property => new KeyValuePair<string, JsonElement>(property.Name, property.Value.Clone())).ToList();
        if (properties.Any(property => property.Key is McpPayloadTargets.ArgumentName or "plan"))
        {
            throw ApiException.Validation("A payload can't carry plan or payload; send them inline.", McpPayloadTargets.ArgumentName);
        }
        return properties;
    }
}
