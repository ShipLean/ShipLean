using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;

namespace ShipLean.UnitTest.Dtos.Mcp;

// The wire shapes of MCP tool results: property names are pinned with JsonPropertyName, so they come out the same
// whatever naming policy the MCP serializer is configured with.
[TestFixture]
public class McpWireTests
{
    private static readonly JsonSerializerOptions[] Serializers =
    [
        new(JsonSerializerDefaults.Web),
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
        new(),
    ];

    private sealed record Created(string? Project, string OpenUrl, string Key) : McpResult(Project, OpenUrl);

    [Test]
    public void McpError_ShouldRoundTrip()
    {
        var error = McpError.Create(ApiErrorCodes.Ambiguous, "Sam is two members.", "assignee", ["Sam Rivera", "Sam Okafor"]);
        var expected = JsonNode.Parse(
            """{"ok":false,"error":{"code":"ambiguous","message":"Sam is two members.","field":"assignee","candidates":["Sam Rivera","Sam Okafor"]}}""")!;

        foreach (var options in Serializers)
        {
            JsonNode.DeepEquals(JsonSerializer.SerializeToNode(error, options), expected).ShouldBeTrue();
            JsonSerializer.Deserialize<McpError>(expected, options).ShouldNotBeNull().Error.Candidates.ShouldBe(["Sam Rivera", "Sam Okafor"]);
        }
    }

    [Test]
    public void McpError_WhenFieldAndCandidatesAbsent_ShouldWriteThemAsNull()
    {
        var json = JsonSerializer.SerializeToNode(McpError.Create(ApiErrorCodes.NotFound, "No such item."))!;

        json["error"]!["field"].ShouldBeNull();
        json["error"]!.AsObject().ContainsKey("candidates").ShouldBeTrue();
        json["ok"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Test]
    public void McpResult_ShouldRoundTripSnakeCase()
    {
        var result = new Created("SHIP", "https://planner.example.com/p/SHIP/items/SHIP-1", "SHIP-1");

        foreach (var options in Serializers)
        {
            var json = JsonSerializer.SerializeToNode(result, options)!;
            json["project"]!.GetValue<string>().ShouldBe("SHIP");
            json["open_url"]!.GetValue<string>().ShouldBe(result.OpenUrl);
            JsonSerializer.Deserialize<McpResult>(json, options).ShouldNotBeNull().OpenUrl.ShouldBe(result.OpenUrl);
        }
    }
}
