using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Agents;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Touch;

// Joel's projects, sessions and connected agents on a phone (SHIP-733 D3, D4, D5). Joel is SHIP's only admin and a member
// of PAY. The touch rules are checked on this task's panels only.
[TestFixture]
public class ProfileAccountTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private ILocator ProjectsPanel => Page.GetByTestId(SettingsTestIds.MyProjectsPanel);

    private ILocator SessionsPanel => Page.GetByTestId(SettingsTestIds.SessionsPanel);

    private ILocator AgentsPanel => Page.GetByTestId(AgentsTestIds.SlotConnectedAgents);

    [Test]
    public async Task Revoke_WhenTappedOnAPhone_ShouldAskFirstInTheRow()
    {
        await Host.IssueAgentToken(TestUser.Joel, clientName: "Asked Bot");
        var agents = new ConnectedAgentsScreen(Page);
        await agents.OpenAsync();
        var row = agents.Row("Asked Bot");

        await row.GetByTestId(AgentsTestIds.ConnectedRevoke).TapAsync();

        var question = Page.GetByTestId(AgentsTestIds.ConnectedRevokeRow);
        await Expect(question).ToContainTextAsync("It stops working straight away, even mid-task.");
        (await TouchRules.ViolationsAsync(Page, AgentsPanel)).ShouldBeEmpty();
        await Page.GetByTestId(AgentsTestIds.ConnectedRevokeCancel).TapAsync();
        await Expect(row).ToHaveCountAsync(1);

        await row.GetByTestId(AgentsTestIds.ConnectedRevoke).TapAsync();
        await Page.GetByTestId(AgentsTestIds.ConnectedRevokeConfirm).TapAsync();

        await Expect(row).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId(ComponentTestIds.Toast).First).ToContainTextAsync("Asked Bot is disconnected.");
    }

    [Test]
    public async Task Leave_WhenTappedOnAPhone_ShouldStackTheConfirmationWithEqualButtons()
    {
        var account = new ProfileAccountPage(Page);
        await account.OpenAsync();
        (await TouchRules.ViolationsAsync(Page, ProjectsPanel)).ShouldBeEmpty();

        await account.ProjectRow("PAY").GetByTestId(SettingsTestIds.MyProjectLeave).TapAsync();

        var cancel = await Page.GetByTestId(SettingsTestIds.MyProjectLeaveCancel).BoundingBoxAsync();
        var leave = await Page.GetByTestId(SettingsTestIds.MyProjectLeaveConfirm).BoundingBoxAsync();
        var text = await Page.GetByTestId(SettingsTestIds.MyProjectLeaveRow).BoundingBoxAsync();
        Math.Abs(cancel!.Width - leave!.Width).ShouldBeLessThanOrEqualTo(1);
        cancel.Y.ShouldBe(leave.Y);
        cancel.Y.ShouldBeGreaterThan(text!.Y + 20);
        (await TouchRules.ViolationsAsync(Page, ProjectsPanel)).ShouldBeEmpty();

        // SHIP has no other admin, so the server refuses and its message shows in the row.
        await Page.GetByTestId(SettingsTestIds.MyProjectLeaveCancel).TapAsync();
        await account.ProjectRow("SHIP").GetByTestId(SettingsTestIds.MyProjectLeave).TapAsync();
        await Page.GetByTestId(SettingsTestIds.MyProjectLeaveConfirm).TapAsync();
        await Expect(account.LeaveConfirmError).ToHaveTextAsync("You are the only admin. Make someone else an admin before you leave.");
    }

    [Test]
    public async Task ClaudeCodeLine_WhenCopiedOnAPhone_ShouldCopyAndToast()
    {
        await Page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        var agents = new ConnectedAgentsScreen(Page);
        await agents.OpenAsync();
        var line = (await agents.AddLine.InnerTextAsync()).Trim();

        await Page.GetByTestId(AgentsTestIds.ConnectedCopy).TapAsync();

        await Expect(Page.GetByTestId(ComponentTestIds.Toast).First).ToContainTextAsync("Copied");
        (await Page.EvaluateAsync<string>("navigator.clipboard.readText()")).ShouldBe(line);
        line.ShouldStartWith("claude mcp add --transport http shiplean ");
    }

    [Test]
    public async Task Account_WhenOnAPhone_ShouldHaveNoSidewaysScroll()
    {
        await Host.IssueAgentToken(TestUser.Joel, clientName: "A Bot With A Fairly Long Name");
        var agents = new ConnectedAgentsScreen(Page);
        await agents.OpenAsync();
        await Expect(ProjectsPanel).ToBeVisibleAsync();
        await Expect(SessionsPanel).ToBeVisibleAsync();

        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();
        // The Claude Code line wraps rather than scrolling inside itself, so nothing in the three panels overflows its box.
        var overflowing = await Page.EvaluateAsync<string[]>(
            "ids => ids.flatMap(id => [...document.querySelectorAll(`[data-testid=\"${id}\"], [data-testid=\"${id}\"] *`)]" +
            ".filter(el => el.scrollWidth > el.clientWidth).map(el => el.getAttribute('data-testid') ?? el.tagName))",
            new[] { SettingsTestIds.MyProjectsPanel, SettingsTestIds.SessionsPanel, AgentsTestIds.SlotConnectedAgents });
        overflowing.ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, SessionsPanel)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, AgentsPanel)).ShouldBeEmpty();
    }
}
