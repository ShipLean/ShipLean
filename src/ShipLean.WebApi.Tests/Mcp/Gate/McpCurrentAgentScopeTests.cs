using System.Net;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Gate;

// The agent is the agent on /mcp and nobody anywhere else: after an agent's call on the same host, a person's /api write is
// not stamped and work with no request (a hub, a background service) sees no agent.
[TestFixture]
public class McpCurrentAgentScopeTests : McpFixture
{
    protected override TestUser SignedInAs => TestUser.Joel;

    [Test]
    public async Task CurrentAgent_WhenAnAgentCalledMcpBefore_ShouldBeNoneOnApiAndOutsideRequests()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);
        var whoami = await CallToolAsync(token, "probe_whoami");
        whoami.ResultText().ShouldContain("|Test Agent|");

        using var created = await Api.PostJsonAsync("api/work-items", new JsonObject
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = "story",
            ["title"] = "A person made this",
            ["parentId"] = null,
            ["sprintId"] = null,
            ["changes"] = null,
        });

        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var connection = await OpenDatabaseAsync();
        var agentNames = await connection.QueryAsync<string?>("SELECT agent_name FROM revision r JOIN work_item w ON w.id = r.work_item_id WHERE w.title = 'A person made this'");
        agentNames.ShouldBe([null]);
        var outside = Host.Services.GetRequiredService<ICurrentAgent>();
        outside.Name.ShouldBeNull();
        outside.Verified.ShouldBeFalse();
    }
}
