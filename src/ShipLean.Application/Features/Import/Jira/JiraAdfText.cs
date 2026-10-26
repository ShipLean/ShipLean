using System.Text;
using System.Text.Json.Nodes;

namespace ShipLean.Application.Features.Import.Jira;

// Jira Cloud sends a description or comment as an Atlassian Document Format tree, Server as text. This flattens the tree to markdown-ish plain text.
public static class JiraAdfText
{
    // Null for no body at all; a string passes through; an ADF document is flattened.
    public static string? From(JsonNode? body) => body switch
    {
        null => null,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        JsonObject document => Blocks(document.Children()),
        _ => null,
    };

    private static string Blocks(IEnumerable<JsonNode> nodes)
        => string.Join("\n\n", nodes.Select(Block).Where(b => b.Length > 0)).Trim();

    private static string Block(JsonNode node)
    {
        var element = node.AsObject();
        var type = (string?)element["type"];
        return type switch
        {
            "heading" => new string('#', Math.Clamp((int?)element["attrs"]?["level"] ?? 1, 1, 6)) + " " + Inline(element),
            "codeBlock" => "```\n" + Inline(element) + "\n```",
            "blockquote" => string.Join('\n', Blocks(element.Children()).Split('\n').Select(line => "> " + line)),
            "bulletList" => List(element, ordered: false),
            "orderedList" => List(element, ordered: true),
            "rule" => "---",
            "table" => string.Join('\n', element.Children().Select(row => string.Join(" | ", row.AsObject().Children().Select(cell => Blocks(cell.AsObject().Children()).Replace('\n', ' '))))),
            "paragraph" => Inline(element),
            _ when element["content"] is JsonArray content && content.Any(c => c is JsonObject child && IsBlock((string?)child["type"])) => Blocks(element.Children()),
            _ => Inline(element),
        };
    }

    private static bool IsBlock(string? type)
        => type is "paragraph" or "heading" or "codeBlock" or "blockquote" or "bulletList" or "orderedList" or "rule" or "table" or "panel" or "expand" or "tableRow" or "tableCell" or "tableHeader" or "listItem";

    private static string List(JsonObject list, bool ordered)
        => string.Join('\n', list.Children().Select((item, index) => (ordered ? $"{index + 1}. " : "- ") + Blocks(item.AsObject().Children()).Replace("\n", "\n  ")));

    private static IEnumerable<JsonNode> Children(this JsonObject element)
        => element["content"] is JsonArray content ? content.OfType<JsonObject>() : [];

    private static string Inline(JsonObject element)
    {
        var text = new StringBuilder();
        foreach (var child in element.Children())
        {
            text.Append(Leaf(child.AsObject()));
        }
        return text.ToString();
    }

    private static string Leaf(JsonObject node) => (string?)node["type"] switch
    {
        "text" => (string?)node["text"] ?? string.Empty,
        "hardBreak" => "\n",
        "mention" or "emoji" => (string?)node["attrs"]?["text"] ?? (string?)node["attrs"]?["shortName"] ?? string.Empty,
        "inlineCard" => (string?)node["attrs"]?["url"] ?? string.Empty,
        "media" or "mediaSingle" or "mediaGroup" => string.Empty,
        _ => Inline(node),
    };
}
