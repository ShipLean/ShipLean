using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Shouldly;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;

namespace ShipLean.WebApi.Tests.Agents;

// The host hook mints a token through the agent interfaces only, and IAgentTokens accepts it. The seed's base_url is
// https://shiplean.larkspur.dev, so that is the audience.
public class AgentTokenHostTests : ApiFixture
{
    private const string SeededResource = "https://shiplean.larkspur.dev/mcp";

    [Test]
    public async Task IssueAgentToken_ShouldValidateThroughIAgentTokens()
    {
        var token = await Host.IssueAgentToken(TestUser.Mei, new HashSet<string> { "shiplean:read", "shiplean:write" }, "Review Bot");

        await using var scope = Host.Services.CreateAsyncScope();
        var principal = await scope.ServiceProvider.GetRequiredService<IAgentTokens>().ValidateAsync(token, SeededResource);
        principal.ShouldNotBeNull();
        principal.UserId.ShouldBe(TestUser.Mei.Id!.Value);
        principal.ClientName.ShouldBe("Review Bot");
        principal.Verified.ShouldBeFalse();
        principal.Scopes.ShouldBe(["shiplean:read", "shiplean:write"], ignoreOrder: true);
    }

    [Test]
    public async Task IssueAgentToken_WhenNoScopes_ShouldGrantRead()
    {
        var token = await Host.IssueAgentToken(TestUser.Priya);

        await using var scope = Host.Services.CreateAsyncScope();
        var principal = await scope.ServiceProvider.GetRequiredService<IAgentTokens>().ValidateAsync(token, SeededResource);
        principal!.Scopes.ShouldBe(["shiplean:read"]);
    }

    [Test]
    public async Task IssueAgentToken_WhenOtherAudience_ShouldNotValidate()
    {
        var token = await Host.IssueAgentToken(TestUser.Mei);

        await using var scope = Host.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IAgentTokens>().ValidateAsync(token, Host.BaseUrl + "/mcp")).ShouldBeNull();
    }

    [Test]
    public async Task IssueAgentToken_ShouldListTheAgentForItsUser()
    {
        await Host.IssueAgentToken(TestUser.Tom, clientName: "Listed Bot");

        await using var scope = Host.Services.CreateAsyncScope();
        var agents = await scope.ServiceProvider.GetRequiredService<IAgentAuthorizations>().ListAsync(TestUser.Tom.Id!.Value);
        agents.ShouldContain(agent => agent.ClientName == "Listed Bot");
    }

    // Nothing serves /mcp successfully yet, so this only pins that the test client takes a host and a bearer.
    [Test]
    public void McpTestClient_ShouldConnectWithAHostAndABearer()
    {
        Func<ShipLeanHost, string, CancellationToken, Task<McpClient>> connect = McpTestClient.ConnectAsync;

        connect.ShouldNotBeNull();
    }
}
