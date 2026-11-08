using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Journeys.Collaboration;
using ShipLean.AcceptanceTests.Pages.QuickSearch;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Shell;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Shell.Touch;

// The touch top bar on a phone against the real server, signed in as Joel (SHIP-713): full-screen quick search with
// Cancel, the breadcrumb cut from the left, and presence as a count. The touch rules are checked on the bar, the
// presence sheet and the quick search dialog only; the page content is other tasks'.
[TestFixture]
public class TouchTopBarTests : SeededAcceptanceTest
{
    private const string SeedWord = "passkey";
    private const string PasskeyItem = "SHIP-129";
    private const string LongFileName = "release-checklist-and-notes.md";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task QuickSearch_WhenSearchTappedOnPhone_ShouldFillScreenAndCancelShouldClose()
    {
        var touch = new TouchShell(Page);
        var search = new QuickSearchDialog(Page);
        using (var scope = Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISearchIndexer>().RunPassAsync(true);
        }

        await Page.GotoAsync(ShellFrame.BacklogPath("SHIP"));
        await Expect(touch.TopBar).ToBeVisibleAsync();

        await touch.TopBarSearch.TapAsync();
        await search.TypeAsync(SeedWord);

        var dialog = (await search.Dialog.BoundingBoxAsync())!;
        (dialog.X, dialog.Y, dialog.Width, dialog.Height).ShouldBe((0f, 0f, 390f, 844f));
        var row = search.Item(PasskeyItem);
        await Expect(row).ToBeVisibleAsync();
        var title = (await row.GetByTestId(QuickSearchTestIds.OptionLabel).BoundingBoxAsync())!;
        var key = (await row.GetByTestId(QuickSearchTestIds.OptionKey).BoundingBoxAsync())!;
        key.Y.ShouldBeGreaterThanOrEqualTo(title.Y + title.Height - 1);
        (await TouchRules.ViolationsAsync(Page, search.Dialog)).ShouldBeEmpty();

        await search.Cancel.TapAsync();

        await Expect(search.Dialog).ToHaveCountAsync(0);
        await Expect(touch.TopBar).ToBeVisibleAsync();
    }

    [Test]
    public async Task Crumbs_WhenTrailTooLongOnPhone_ShouldKeepScreenNameVisible()
    {
        // The Sprint retro trail (SHIP › Sprints › Sprint 24 › Retro) fits at 390px once Live is a dot, so this opens an
        // attachment page, whose last crumb is the file's name on a phone: SHIP › Backlogs › SHIP-101 › <name>.
        var touch = new TouchShell(Page);
        var attachmentId = await AddMarkdownAttachmentAsync(SeedIds.WorkItems.Ship101, LongFileName);
        await Page.GotoAsync($"/p/SHIP/items/SHIP-101/attachments/{attachmentId}");
        var crumbs = touch.TopBarTitle.GetByTestId(ShellTestIds.Crumb);
        await Expect(crumbs.Last).ToHaveTextAsync(LongFileName);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await Expect(touch.TopBarTitle.GetByTestId(ShellTestIds.Crumbs)).ToHaveAttributeAsync("data-cut", "true");

        // The layout boxes, not BoundingBoxAsync: Playwright clips that to the visible part, so a cut crumb looks whole.
        var bar = await LeftAndRightAsync(touch.TopBarTitle);
        var first = await LeftAndRightAsync(crumbs.First);
        var last = await LeftAndRightAsync(crumbs.Last);
        first.Left.ShouldBeLessThan(bar.Left);
        last.Left.ShouldBeGreaterThanOrEqualTo(bar.Left);
        last.Right.ShouldBeLessThanOrEqualTo(bar.Right + 0.5);
        (await TouchRules.ViolationsAsync(Page, touch.TopBar)).ShouldBeEmpty();
    }

    [Test]
    public async Task Presence_WhenOthersAreHereOnPhone_ShouldShowACountButtonAtLeast32px()
    {
        var touch = new TouchShell(Page);
        await Page.GotoAsync(ShellFrame.BacklogPath("SHIP"));
        await Expect(touch.TopBar).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();

        await using var tom = await PresenceViewer.SignInAsTomAsync(Page, BaseUrl);
        await tom.Page.GotoAsync(ShellFrame.BacklogPath("SHIP"));

        var count = touch.TopBar.GetByTestId(RealtimeTestIds.PresenceCount);
        await Expect(count).ToHaveTextAsync("+1");
        await Expect(Page.GetByTestId(RealtimeTestIds.PresenceAvatar)).ToHaveCountAsync(0);
        var box = (await count.BoundingBoxAsync())!;
        box.Width.ShouldBeGreaterThanOrEqualTo(32);
        box.Height.ShouldBeGreaterThanOrEqualTo(32);
        (await TouchRules.ViolationsAsync(Page, touch.TopBar)).ShouldBeEmpty();

        await count.TapAsync();

        await Expect(touch.Sheets.GetByTestId(RealtimeTestIds.PresenceSheetItem)).ToContainTextAsync(["Tom"]);
        (await TouchRules.ViolationsAsync(Page, touch.Sheets)).ShouldBeEmpty();
    }

    private async Task<Guid> AddMarkdownAttachmentAsync(Guid itemId, string name)
    {
        var response = await Page.APIRequest.PostAsync($"/api/work-items/{itemId}/attachments", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "touch-top-bar-tests" },
            Multipart = Page.APIRequest.CreateFormData().Set("file", new FilePayload { Name = name, MimeType = "text/markdown", Buffer = "# Release checklist\n"u8.ToArray() }),
        });
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonNode.Parse(await response.TextAsync())!["id"]!.GetValue<Guid>();
    }

    private static async Task<(double Left, double Right)> LeftAndRightAsync(ILocator locator)
    {
        var edges = await locator.EvaluateAsync<double[]>("e => { const b = e.getBoundingClientRect(); return [b.left, b.right]; }");
        return (edges[0], edges[1]);
    }
}
