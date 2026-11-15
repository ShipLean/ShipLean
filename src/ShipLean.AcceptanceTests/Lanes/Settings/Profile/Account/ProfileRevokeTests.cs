using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Agents;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Account;

// Revoke asks first at every width (SHIP-733 D4); this is the desktop.
[TestFixture]
public class ProfileRevokeTests : SeededAcceptanceTest
{
    [Test]
    public async Task Revoke_WhenClickedOnTheDesktop_ShouldAskFirst()
    {
        await Host.IssueAgentToken(TestUser.Joel, clientName: "Asked Bot");
        var panel = new ConnectedAgentsScreen(Page);
        await panel.OpenAsync();
        var row = panel.Row("Asked Bot");

        await row.GetByTestId(AgentsTestIds.ConnectedRevoke).ClickAsync();

        var question = Page.GetByTestId(AgentsTestIds.ConnectedRevokeRow);
        await Expect(question).ToContainTextAsync("Revoke Asked Bot? It stops working straight away, even mid-task. To use it again, connect it again.");
        await Page.GetByTestId(AgentsTestIds.ConnectedRevokeCancel).ClickAsync();
        await Expect(question).ToHaveCountAsync(0);
        await Expect(row).ToHaveCountAsync(1);

        await row.GetByTestId(AgentsTestIds.ConnectedRevoke).ClickAsync();
        await Page.GetByTestId(AgentsTestIds.ConnectedRevokeConfirm).ClickAsync();

        await Expect(row).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId(ComponentTestIds.Toast).First).ToContainTextAsync("Asked Bot is disconnected.");
    }
}
