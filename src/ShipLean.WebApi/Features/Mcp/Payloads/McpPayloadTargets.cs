using System.Text.Json;
using ModelContextProtocol.Server;

namespace ShipLean.WebApi.Features.Mcp.Payloads;

// Which tools take a payload and which of their arguments it may fill. A tool opts in with [McpPayload] on its method (the
// SDK copies method attributes into McpServerTool.Metadata, as it does [McpScope]); its text fields are the string
// properties of its input schema, other than the payload itself, the plan token and the project.
public static class McpPayloadTargets
{
    public const string ArgumentName = "payload";

    // The HttpContext.Items key the payload filter sets when a call used a payload, so usage tracking can count it.
    public const string UsedItem = "shiplean.mcp.payload-used";

    public const string ArgumentDescription = "The id create_payload_upload returned, in place of large arguments you uploaded from a file. Arguments you also send inline win over the payload's.";

    private static readonly HashSet<string> NotText = [ArgumentName, "plan", "project"];

    public static PayloadTarget? For(McpServerTool tool)
    {
        if (!tool.Metadata.OfType<McpPayloadAttribute>().Any())
        {
            return null;
        }
        var fields = new HashSet<string>(StringComparer.Ordinal);
        if (tool.ProtocolTool.InputSchema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (!NotText.Contains(property.Name) && IsString(property.Value))
                {
                    fields.Add(property.Name);
                }
            }
        }
        return new PayloadTarget(tool.ProtocolTool.Name, fields);
    }

    public static IReadOnlyList<PayloadTarget> All(McpServerToolCollection tools)
        => tools.Select(For).OfType<PayloadTarget>().ToList();

    // "type": "string", or "type": ["string", "null"].
    private static bool IsString(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return false;
        }
        return type.ValueKind switch
        {
            JsonValueKind.String => type.GetString() == "string",
            JsonValueKind.Array => type.EnumerateArray().All(t => t.GetString() is "string" or "null") && type.EnumerateArray().Any(t => t.GetString() == "string"),
            _ => false,
        };
    }
}
