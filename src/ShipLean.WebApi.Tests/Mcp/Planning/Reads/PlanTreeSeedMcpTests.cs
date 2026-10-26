using ModelContextProtocol.Client;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Reads;

// The recipe for seeding a plan in a McpFixture class's [OneTimeSetUp]: act as a project member first (McpFixture signs in as
// Anonymous and nobody is acting in a [OneTimeSetUp]), then seed, then call the planning tools over a bearer.
[TestFixture]
public class PlanTreeSeedMcpTests : McpFixture
{
    private PlanSeedKeys _keys = null!;

    [OneTimeSetUp]
    public async Task SeedPlanAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        _keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
    }

    [Test]
    public async Task PlanTreeSeed_FromMcpFixtureOneTimeSetUp_ShouldSeed()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        await using var client = await McpTestClient.ConnectAsync(Host, token);

        var result = await client.CallToolAsync("plan_status", new Dictionary<string, object?> { ["item"] = _keys.Feature });

        result.IsError.ShouldNotBe(true);
    }
}
