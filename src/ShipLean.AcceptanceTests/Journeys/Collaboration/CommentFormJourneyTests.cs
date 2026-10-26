using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Discussion;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Collaboration;

// Comments are their own writes: neither Joel's own comment nor Tom's marks the form unsaved, touches an edit in
// progress, or makes the next save conflict.
[TestFixture]
public class CommentFormJourneyTests : SeededAcceptanceTest
{
    [Test]
    public async Task J5_CommentAdded_DoesNotDirtyForm()
    {
        var screen = new WorkItemScreen(Page);
        var discussion = new DiscussionPanel(Page);
        var side = new SideRailPanel(Page);
        var conflict = new ConflictPanel(Page);
        var itemId = SeedIds.WorkItems.Ship141;
        await screen.OpenAsync("SHIP", "SHIP-141");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var comments = (await WorkItemComments.ListAsync(Page, itemId)).GetArrayLength();
        await Expect(discussion.Comments).ToHaveCountAsync(comments);
        await Expect(side.Revisions).ToHaveCountAsync(1);

        // 1. A clean form stays clean, and leaving is not blocked.
        (await discussion.PostAsync(itemId, "First thoughts on the vault")).Ok.ShouldBeTrue();
        await Expect(discussion.Comments).ToHaveCountAsync(comments + 1);
        await Expect(discussion.Comments.Last).ToContainTextAsync("First thoughts on the vault");
        await Expect(screen.Save).ToBeDisabledAsync();
        await screen.Back.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(Regex.Escape(Links.Backlog("SHIP")) + "$"));
        await screen.OpenAsync("SHIP", "SHIP-141");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await Expect(discussion.Comments).ToHaveCountAsync(comments + 1);

        // 2. An unsaved title survives Joel's own comment, and Save then writes the title alone.
        var original = await screen.Title.InputValueAsync();
        await screen.Title.FillAsync("Vault, reworded");
        await Expect(screen.Save).ToBeEnabledAsync();
        (await discussion.PostAsync(itemId, "Second thoughts")).Ok.ShouldBeTrue();
        await Expect(discussion.Comments).ToHaveCountAsync(comments + 2);
        await Expect(screen.Title).ToHaveValueAsync("Vault, reworded");
        await Expect(screen.Save).ToBeEnabledAsync();
        await Expect(screen.SaveState).ToHaveAttributeAsync("title", new Regex("^Unsaved changes to Title$"));
        (await screen.SaveAsync(itemId)).Ok.ShouldBeTrue();
        await Expect(side.Revisions).ToHaveCountAsync(2);
        await Expect(side.Revisions.First.GetByTestId(WorkItemTestIds.RevisionChange)).ToHaveCountAsync(1);
        await Expect(side.Revisions.First.GetByTestId(WorkItemTestIds.RevisionChange)).ToContainTextAsync("Title");
        await Expect(screen.Save).ToBeDisabledAsync();

        // 3. Tom's comment arrives live beside a new unsaved edit, which stays Joel's alone and saves without a conflict.
        await screen.Title.FillAsync(original);
        await Expect(screen.Save).ToBeEnabledAsync();
        await WorkItemComments.AddAsTomAsync(Page, BaseUrl, itemId, "Tom here, looks fine to me");
        await Expect(discussion.Comments).ToHaveCountAsync(comments + 3);
        await Expect(discussion.Comments.Last).ToContainTextAsync("Tom here, looks fine to me");
        await Expect(screen.Title).ToHaveValueAsync(original);
        await Expect(screen.SaveState).ToHaveAttributeAsync("title", new Regex("^Unsaved changes to Title$"));
        await Expect(conflict.Banner).ToHaveCountAsync(0);
        var saved = await screen.SaveAsync(itemId);
        saved.Ok.ShouldBeTrue();
        await Expect(conflict.Banner).ToHaveCountAsync(0);
        await Expect(screen.Save).ToBeDisabledAsync();
        await Expect(side.Revisions).ToHaveCountAsync(3);
        await Expect(screen.Title).ToHaveValueAsync(original);
    }
}
