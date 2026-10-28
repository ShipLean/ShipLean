using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// Every tool result on the wire, errors included. content is the summary line, then the Markdown a result is about (a
// brief, a guide part), then the same JSON as structuredContent: a client that shows the model only text still gets the
// whole result, as the MCP spec asks of a tool that returns structured content.
public static class McpToolResult
{
    public static CallToolResult Of(JsonElement structured, string summary, string? markdown = null, bool isError = false)
    {
        List<ContentBlock> content = [new TextContentBlock { Text = summary }];
        if (markdown is not null)
        {
            content.Add(new TextContentBlock { Text = markdown });
        }
        content.Add(new TextContentBlock { Text = structured.GetRawText() });
        return new CallToolResult
        {
            IsError = isError ? true : null,
            Content = content,
            StructuredContent = structured,
        };
    }
}
