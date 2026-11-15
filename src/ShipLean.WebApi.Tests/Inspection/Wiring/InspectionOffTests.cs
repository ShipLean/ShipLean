using System.Net;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// SHIPLEAN_INSPECTION unset: every K5 route is not_found, even for the project's admin.
[TestFixture]
public class InspectionOffTests : McpFixture
{
    protected override TestUser SignedInAs => TestUser.Joel;

    [TestCase("api/inspection/projects/SHIP/items/SHIP-1/runs")]
    [TestCase("api/inspection/projects/SHIP/items/SHIP-1/runs/sess-1")]
    [TestCase("api/inspection/projects/SHIP/items/SHIP-1/runs/sess-1/rows/r1")]
    [TestCase("api/inspection/projects/SHIP/settings")]
    [TestCase("api/inspection/projects/SHIP/agent-setup?agent=claude-code")]
    public async Task Get_WhenInstanceOff_ShouldBeNotFound(string url)
    {
        using var response = await Api.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Health_WhenInstanceOff_ShouldSayDisabled()
    {
        using var response = await Api.GetAsync("api/inspection/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"enabled\":false");
    }

    [Test]
    public async Task ToolsList_WhenInstanceOff_ShouldLackTheInspectionTools()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        reply.Body.ShouldContain("list_retrospectives");
        reply.Body.ShouldNotContain("list_runs");
        reply.Body.ShouldNotContain("get_run");
        reply.Body.ShouldNotContain("agent_setup");
    }

    [TestCase("list_runs", """{"item":"SHIP-1"}""")]
    [TestCase("get_run", """{"item":"SHIP-1","run":"r"}""")]
    [TestCase("agent_setup", """{"item":"SHIP-1","agent":"claude-code"}""")]
    public async Task ToolsCall_WhenInstanceOff_ShouldBeNotFound(string tool, string arguments)
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, tool, arguments);

        reply.Body.ShouldContain("not_found");
    }
}
