using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.InspectionMetrics;

// inspection_metrics over the real read (SHIP-1398 K4): the tool answers with the endpoint's numbers in MCP naming.
[TestFixture]
public class InspectionMetricsToolTests : McpFixture
{
    protected override bool WithProbeTools => false;

    // Mutation: pass InspectionRange.All to the service whatever the input.
    [Test]
    public async Task Call_WhenMember_ShouldReturnSameNumbersAsEndpoint()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "inspection_metrics", """{"project":"SHIP","range":"lastQuarter","cut":"who"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var body = JsonNode.Parse(reply.Structured().GetRawText()).ShouldNotBeNull().AsObject();
        body["project"]!["key"]!.GetValue<string>().ShouldBe("SHIP");
        body["open_url"]!.GetValue<string>().ShouldEndWith("/p/SHIP/inspection");
        body.ContainsKey("generated_at").ShouldBeTrue();
        body.Remove("open_url");
        var fromTool = body.Deserialize<InspectionMetricsViewDto>(InspectionMetricsJson.McpOptions)!;

        var webOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        using var response = await Api.ActingAs(TestUser.Joel).GetAsync($"api/projects/{SeedIds.Projects.Ship}/inspection-metrics?range=lastQuarter&cut=who");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, text);
        var fromApi = JsonSerializer.Deserialize<InspectionMetricsViewDto>(text, webOptions).ShouldNotBeNull();

        fromTool.Range.ShouldBe(InspectionRange.LastQuarter);
        fromTool.Cut.ShouldBe(InspectionCut.Who);
        JsonSerializer.Serialize(fromTool with { GeneratedAt = fromApi.GeneratedAt }, webOptions)
            .ShouldBe(JsonSerializer.Serialize(fromApi, webOptions));
    }

    [Test]
    public async Task Call_WhenNotMember_ShouldReturnNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "inspection_metrics", """{"project":"PART"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }
}
