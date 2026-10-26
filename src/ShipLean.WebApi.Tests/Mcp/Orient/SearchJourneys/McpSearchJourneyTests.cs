using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;
using ShipLean.WebApi.Tests.Search.Journeys;

namespace ShipLean.WebApi.Tests.Mcp.Orient.SearchJourneys;

// The MCP search tool over rows written through the real API and a full reindex, with no project named.
[TestFixture]
public class McpSearchJourneyTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public async Task McpSearch_WhenWordsAcrossProjects_ShouldFindIt()
    {
        var joel = Api.ActingAs(TestUser.Joel);
        var inShip = await JourneyApi.CreateItemAsync(joel, SeedIds.Projects.Ship, "Okapi ledger reconciliation");
        var inPay = await JourneyApi.CreateItemAsync(joel, SeedIds.Projects.Pay, "Okapi refund ledger audit");
        await JourneyApi.ReindexAsync(joel);

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "search", """{"words":"okapi ledger"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var matches = reply.Structured().GetProperty("matches").EnumerateArray().Where(m => m.GetProperty("kind").GetString() == "item").ToList();
        matches.Single(m => m.GetProperty("key").GetString() == inShip.Key).GetProperty("project").GetString().ShouldBe("SHIP");
        matches.Single(m => m.GetProperty("key").GetString() == inPay.Key).GetProperty("project").GetString().ShouldBe("PAY");
    }
}
