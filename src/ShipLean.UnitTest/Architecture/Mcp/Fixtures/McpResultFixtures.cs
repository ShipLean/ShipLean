using System.Text.Json;
using ModelContextProtocol.Protocol;
using ShipLean.WebApi.Features.Mcp.Transport;

// Reply helpers that break (or keep) the one-result-shape rule on purpose, under a namespace the production rule does not scan.
namespace ShipLean.UnitTest.Architecture.Mcp.Fixtures.Results
{
    internal static class HandBuiltReply
    {
        public static CallToolResult Of(string summary) => new() { Content = [new TextContentBlock { Text = summary }] };
    }

    internal static class SharedReply
    {
        public static CallToolResult Of(JsonElement result, string summary) => McpToolResult.Of(result, summary);
    }
}
