using System.Text.Json;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Linking;

// How a run links to items (K3): by the item attribute the launcher set, or by a ShipLean MCP tool call naming the item.
// Pure, so each store can translate the same rules into its own query and the contract suite can check them.
public static partial class LinkRules
{
    private const string LinkSourceAttribute = "shiplean.link_source";
    private const string FileSource = "file";
    private const string McpServerAttribute = "mcp_server_name";
    private const string ToolParametersAttribute = "tool_parameters";
    private const string ShipleanServer = "shiplean";
    private const string ToolNameAttribute = "tool_name";
    private const string McpToolName = "mcp_tool";

    private static readonly string[] ToolNameAttributes = ["tool_name"];
    // Claude Code puts the call's arguments in tool_input (OTEL_LOG_TOOL_DETAILS on) and, for an MCP tool, the server in tool_parameters.
    private static readonly string[] InputAttributes = ["tool_input", "tool_parameters", "input"];
    private static readonly string[] ItemProperties = ["item", "key", "task"];

    // Every item a run links to, one link per key, the strongest source first: Tagged, then ShipleanFile, then FromToolCall.
    public static IReadOnlyList<RunItemLink> Derive(IReadOnlyList<SpanRecord> spans, IReadOnlyList<LogRecordData> logs, string itemField)
    {
        var found = new Dictionary<string, LinkSource>();
        foreach (var span in spans)
        {
            Add(found, Tagged(span.Attributes, itemField), TaggedSource(span.Attributes, itemField));
            Add(found, ToolCallItem(span.Attributes, span.Name), LinkSource.FromToolCall);
        }
        foreach (var attributes in logs.Select(log => log.Attributes))
        {
            Add(found, Tagged(attributes, itemField), TaggedSource(attributes, itemField));
            Add(found, ToolCallItem(attributes, null), LinkSource.FromToolCall);
        }
        return [.. found.OrderBy(link => link.Key, StringComparer.Ordinal).Select(link => new RunItemLink(link.Key, link.Value))];
    }

    // Tagged, or ShipleanFile when the Cursor hook script marked the link as coming from a .shiplean file; null when the item
    // attribute is missing or blank.
    public static LinkSource? TaggedSource(IReadOnlyDictionary<string, string> attributes, string itemField)
    {
        if (Tagged(attributes, itemField) is null)
        {
            return null;
        }
        return attributes.GetValueOrDefault(LinkSourceAttribute) == FileSource ? LinkSource.ShipleanFile : LinkSource.Tagged;
    }

    public static string? Tagged(IReadOnlyDictionary<string, string> attributes, string itemField)
        => attributes.TryGetValue(itemField, out var key) && !string.IsNullOrWhiteSpace(key) ? key : null;

    // A ShipLean MCP tool: the tool name matches the pattern exactly (not by suffix), or the MCP server is shiplean.
    public static bool IsShipleanTool(string? toolName, string? mcpServerName)
        => mcpServerName == ShipleanServer || (toolName is not null && ShipleanToolPattern().IsMatch(toolName));

    // The MCP server a record names: the mcp_server_name attribute (Codex CLI, Cursor), else, only when tool_name is "mcp_tool" (Claude
    // Code's tool_result log), the mcp_server_name inside the tool_parameters JSON. Any other tool's parameters are the agent's own
    // text and could claim to be the shiplean server. Empty counts as absent. Null when neither names one.
    public static string? McpServer(IReadOnlyDictionary<string, string> attributes)
    {
        var named = attributes.GetValueOrDefault(McpServerAttribute);
        if (!string.IsNullOrEmpty(named))
        {
            return named;
        }
        return attributes.GetValueOrDefault(ToolNameAttribute) == McpToolName
            ? NullIfEmpty(JsonString(attributes.GetValueOrDefault(ToolParametersAttribute), McpServerAttribute))
            : null;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // The item a ShipLean tool call names: tool name from attribute tool_name, else the span name; input from the first of
    // tool_input, tool_parameters, input that is JSON with a top-level item, key or task string. It is a ShipLean tool when the name
    // matches the pattern or the MCP server is shiplean. Null when it is not a ShipLean tool call or names no item. Each record is judged
    // alone: Claude Code's tool_result log has the server and the input, so no span has to be joined. Text such as prompts is never read.
    public static string? ToolCallItem(IReadOnlyDictionary<string, string> attributes, string? spanName)
    {
        var toolName = ToolNameAttributes.Select(a => attributes.GetValueOrDefault(a)).FirstOrDefault(v => v is not null) ?? spanName;
        if (!IsShipleanTool(toolName, McpServer(attributes)))
        {
            return null;
        }
        return InputAttributes.Select(a => attributes.GetValueOrDefault(a)).Where(v => v is not null).Select(v => ItemOf(v!)).FirstOrDefault(item => item is not null);
    }

    private static string? JsonString(string? json, string property)
    {
        if (json is null)
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            // Free text from an agent, not JSON: it names no server.
            return null;
        }
    }

    private static string? ItemOf(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            foreach (var name in ItemProperties)
            {
                if (document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    // An empty string names no item, as in the SQL; the next input is tried.
                    return NullIfEmpty(value.GetString());
                }
            }
            return null;
        }
        catch (JsonException)
        {
            // The input is free text from an agent, not ShipLean's own JSON: it names no item.
            return null;
        }
    }

    private static void Add(Dictionary<string, LinkSource> found, string? key, LinkSource? source)
    {
        if (source is not { } value || key is null)
        {
            return;
        }
        if (!found.TryGetValue(key, out var existing) || Rank(value) < Rank(existing))
        {
            found[key] = value;
        }
    }

    private static int Rank(LinkSource source) => source switch
    {
        LinkSource.Tagged => 0,
        LinkSource.ShipleanFile => 1,
        _ => 2,
    };

    [GeneratedRegex("^(mcp__)?shiplean__(pick_up|report_task|update_progress|get_brief|get_work_item|hand_back|comment|record_decisions|log_retrospective)\\z")]
    private static partial Regex ShipleanToolPattern();
}
