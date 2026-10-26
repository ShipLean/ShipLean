using System.Text.Json;
using ModelContextProtocol.Server;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Scopes;

// The scope one call to a tool needs. The tool's [McpScope] sits on its method or its type (the method wins; the SDK lists
// type attributes before method attributes). A preview, a call to a tool with a PreviewScope and no `plan` argument (and, with
// PreviewWhenManyOf, more than one entry in that array), needs only that scope. A tool with no [McpScope] is a bug the guard-rail test catches; here it fails loudly rather than open.
public static class ToolScopes
{
    public static string Required(McpServerTool tool, IDictionary<string, JsonElement>? arguments)
    {
        var attribute = tool.Metadata.OfType<McpScopeAttribute>().LastOrDefault()
                        ?? throw new InvalidOperationException($"Tool '{tool.ProtocolTool.Name}' has no [McpScope].");
        return IsPreview(attribute, arguments) && attribute.PreviewScope is { } preview ? preview : attribute.Scope;
    }

    // No `plan`, and, when the attribute names an array argument, that array holds more than one entry.
    private static bool IsPreview(McpScopeAttribute attribute, IDictionary<string, JsonElement>? arguments)
    {
        if (ToolCall.PlanGivenIn(arguments))
        {
            return false;
        }
        if (attribute.PreviewWhenManyOf is not { } name)
        {
            return true;
        }
        return arguments is not null
               && arguments.TryGetValue(name, out var many)
               && many.ValueKind == JsonValueKind.Array
               && many.GetArrayLength() > 1;
    }

    public static bool IsWriting(string requiredScope) => requiredScope != McpScopes.Read;
}
