using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// A raw answer from /mcp: the status, the headers and the body, with the JSON-RPC message pulled out of an event stream.
public sealed record McpReply(HttpStatusCode Status, HttpResponseHeaders Headers, string Body)
{
    public string WwwAuthenticate => Headers.WwwAuthenticate.ToString();

    // The body as JSON; for a text/event-stream body, the data of its first event.
    public JsonElement Json()
    {
        var text = Body.TrimStart().StartsWith('{') ? Body : string.Concat(Body.Split('\n').Where(l => l.StartsWith("data:", StringComparison.Ordinal)).Take(1).Select(l => l[5..].Trim()));
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    // The structured content of a tools/call result.
    public JsonElement Structured() => Json().GetProperty("result").GetProperty("structuredContent");

    public bool IsToolError() => Json().GetProperty("result").TryGetProperty("isError", out var flag) && flag.GetBoolean();

    public string ResultText() => Json().GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;

    // Every text block of a tools/call result, in order: all that a client showing the model only text gets.
    public IReadOnlyList<string> Texts()
        => Json().GetProperty("result").GetProperty("content").EnumerateArray().Select(block => block.GetProperty("text").GetString()!).ToList();

    // The result as such a client reads it: the JSON in the last text block.
    public JsonElement TextJson() => JsonDocument.Parse(Texts()[^1]).RootElement.Clone();
}
