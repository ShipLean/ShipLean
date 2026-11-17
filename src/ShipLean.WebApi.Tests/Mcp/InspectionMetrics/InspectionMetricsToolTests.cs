using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.InspectionMetrics;
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

// inspection_metrics with Inspection (agent runs) on for the instance and project SHIP, and a FakeTaskTokenTotals for the store's:
// the tool carries the view's cost in snake_case (SHIP-1398 CK2).
[TestFixture]
public class InspectionMetricsCostToolTests : McpFixture
{
    private static readonly string[] CostKeys = ["available", "note", "periods", "quarters", "range", "headline", "most_tokens", "by_feature"];

    protected override bool WithProbeTools => false;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_INSPECTION"] = "on",
        ["SHIPLEAN_INSPECTION_STORE"] = "tempo-loki",
        ["SHIPLEAN_INSPECTION_TEMPO_URL"] = "http://127.0.0.1:9/tempo",
        ["SHIPLEAN_INSPECTION_LOKI_URL"] = "http://127.0.0.1:9/loki",
    };

    protected override Action<IServiceCollection>? ConfigureTestServices
    {
        get
        {
            var mcp = base.ConfigureTestServices;
            return services =>
            {
                mcp?.Invoke(services);
                services.RemoveAll<ITaskTokenTotals>();
                services.AddSingleton<ITaskTokenTotals>(new FakeTaskTokenTotals());
            };
        }
    }

    // Mutation: serialise the view with the web options (mostTokens instead of most_tokens).
    [Test]
    public async Task Call_WhenProjectAgentRunsOn_ShouldReturnCostInSnakeCase()
    {
        using var put = await Api.ActingAs(TestUser.Joel).PutJsonAsync("api/inspection/projects/SHIP/settings", new InspectionProjectSettingsDto(true, false, null, null, "tempoLoki"));
        put.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "inspection_metrics", """{"project":"SHIP"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var cost = JsonNode.Parse(reply.Structured().GetRawText()).ShouldNotBeNull()["cost"].ShouldNotBeNull().AsObject();
        cost.Select(p => p.Key).ShouldBe(CostKeys, ignoreOrder: true);
        cost["headline"].ShouldNotBeNull().AsObject().Select(p => p.Key).ShouldBe(["finished", "so_far", "quarter_to_date", "last_quarter"], ignoreOrder: true);
    }
}
