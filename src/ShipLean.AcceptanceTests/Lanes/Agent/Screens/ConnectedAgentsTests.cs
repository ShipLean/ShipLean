using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Agents;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Agent.Screens;

// Connected agents on Your profile. Joel is signed in; the seed's base_url is https://shiplean.larkspur.dev.
[TestFixture]
public class ConnectedAgentsTests : SeededAcceptanceTest
{
    [Test]
    public async Task ConnectedAgents_ShouldListAndRevoke()
    {
        await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write" }, "Listed Bot");
        await Host.IssueAgentToken(TestUser.Joel, clientName: "Revoked Bot");
        var panel = new ConnectedAgentsScreen(Page);
        await panel.OpenAsync();

        var listed = panel.Row("Listed Bot");
        await Expect(listed).ToHaveCountAsync(1);
        await Expect(listed.GetByTestId(AgentsTestIds.ConnectedHost)).ToHaveTextAsync("unverified");
        await Expect(listed.GetByTestId(AgentsTestIds.ConnectedScopes)).ToHaveTextAsync("Read · Write");
        await Expect(listed.GetByTestId(AgentsTestIds.ConnectedLastUsed)).ToHaveTextAsync("Never used");
        await Expect(listed.GetByTestId(AgentsTestIds.ConnectedConnectedAt)).ToHaveTextAsync("Connected just now");
        await Expect(panel.Row("Revoked Bot")).ToHaveCountAsync(1);

        await panel.Row("Revoked Bot").GetByTestId(AgentsTestIds.ConnectedRevoke).ClickAsync();

        await Expect(panel.Row("Revoked Bot")).ToHaveCountAsync(0);
        await Expect(listed).ToHaveCountAsync(1);
        var remaining = await PendingConsent.ConnectedAsync(Host, TestUser.Joel);
        remaining.ShouldContain(agent => agent.ClientName == "Listed Bot");
        remaining.ShouldNotContain(agent => agent.ClientName == "Revoked Bot");
    }

    [Test]
    public async Task ConnectedAgents_ShouldShowAddLineFromBaseUrl()
    {
        var panel = new ConnectedAgentsScreen(Page);
        await panel.OpenAsync();

        await Expect(panel.AddLine).ToHaveTextAsync("claude mcp add --transport http shiplean https://shiplean.larkspur.dev/mcp");
    }
}
