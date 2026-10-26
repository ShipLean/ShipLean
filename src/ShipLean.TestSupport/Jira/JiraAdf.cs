using System.Text;
using System.Text.Json.Nodes;

namespace ShipLean.TestSupport.Jira;

// Atlassian Document Format (what Jira Cloud sends for a description or comment body) flattened to plain text, which is what Jira Server sends.
public static class JiraAdf
{
    public static string ToText(JsonNode document)
    {
        var text = new StringBuilder();
        Append(document, text);
        return text.ToString().Trim();
    }

    private static void Append(JsonNode? node, StringBuilder text)
    {
        if (node is not JsonObject element)
        {
            return;
        }
        if ((string?)element["type"] == "text")
        {
            text.Append((string?)element["text"]);
            return;
        }
        foreach (var child in element["content"]?.AsArray() ?? [])
        {
            Append(child, text);
        }
        if ((string?)element["type"] == "paragraph")
        {
            text.Append('\n');
        }
    }
}
