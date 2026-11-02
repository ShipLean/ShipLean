using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Filling;

// Pasting many lines into the Add many review on the seeded Storefront project (SHIP). Each test reads the order back
// from the server. Tests run in order and use titles of their own.
[TestFixture]
public class PasteManyTests : SeededAcceptanceTest
{
    [Test]
    [Order(1)]
    public async Task Paste_WhenReviewed_ShouldCreateStoryWithTasksInOrder()
    {
        var backlog = new StoriesBacklog(Page);
        var review = new PasteReview(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();

        await backlog.AddMany.ClickAsync();
        await review.FillAsync("Survey story (5 pts)\n  Draft questions\n  Wire the form (2 pts)\nbug: Survey submit hangs\nSecond survey story");
        await Expect(review.Rows).ToHaveCountAsync(5);
        foreach (var width in new[] { 1280, 1440 })
        {
            (await FillingSteps.OverflowsAtAsync(Page, backlog, review.Dialog, width)).ShouldBeFalse($"overflow at {width}");
        }

        var response = await review.CreateAsync();

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var keys = await FillingSteps.CreatedKeysAsync(response);
        keys.Count.ShouldBe(5);
        var stack = await backlog.StackAsync();
        // No row had focus, so they go to the top: the story, the bug, the other story. Tasks are not backlog rows.
        stack.Take(3).Select(e => e.Title).ShouldBe(["Survey story", "Survey submit hangs", "Second survey story"]);
        stack[1].Type.ShouldBe("bug");
        stack.Take(3).Select(e => e.Key).ShouldBe([keys[0], keys[3], keys[4]]);
        foreach (var (key, title) in new[] { (keys[1], "Draft questions"), (keys[2], "Wire the form") })
        {
            var task = await BacklogWrites.GetItemAsync(Page.Context.APIRequest, key);
            task.GetProperty("title").GetString().ShouldBe(title);
            task.GetProperty("parent").GetProperty("key").GetString().ShouldBe(keys[0]);
        }

        (await BacklogWrites.GetItemAsync(Page.Context.APIRequest, keys[0])).GetProperty("points").GetDouble().ShouldBe(5);
    }

    [Test]
    [Order(2)]
    public async Task Paste_WhenDuplicateFlagged_ShouldStillCreateIfKept()
    {
        var backlog = new StoriesBacklog(Page);
        var review = new PasteReview(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();

        // The usual way in: two lines pasted into the new-item row with a real clipboard paste.
        await Page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await backlog.NewItem.ClickAsync();
        await backlog.AddTitle.FocusAsync();
        await Page.EvaluateAsync("text => navigator.clipboard.writeText(text)", "Pay with saved card\nA brand new idea");
        await backlog.AddTitle.PressAsync("Control+V");
        await Expect(review.Dialog).ToBeVisibleAsync();
        await Expect(review.Duplicates).ToHaveCountAsync(1);
        await Expect(review.Rows.First.GetByTestId(BacklogTestIds.PasteRowDuplicate)).ToContainTextAsync("SHIP-141");
        await Expect(review.Create).ToBeEnabledAsync();

        var response = await review.CreateAsync();

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var stack = await backlog.StackAsync();
        stack.Take(2).Select(e => e.Title).ShouldBe(["Pay with saved card", "A brand new idea"]);
        stack.Count(e => e.Title == "Pay with saved card").ShouldBe(2);
    }

    [Test]
    [Order(3)]
    public async Task Paste_WhenPutAtTheBottom_ShouldLandLastInOrder()
    {
        var backlog = new StoriesBacklog(Page);
        var review = new PasteReview(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();

        await backlog.AddMany.ClickAsync();
        await review.FillAsync("Bottom one\nBottom two");
        await review.Place.SelectOptionAsync(new SelectOptionValue { Label = "Bottom of the backlog" });
        var response = await review.CreateAsync();

        response.Ok.ShouldBeTrue(await response.TextAsync());
        ((await backlog.StackAsync()).TakeLast(2)).Select(e => e.Title).ShouldBe(["Bottom one", "Bottom two"]);
    }

    [Test]
    [Order(4)]
    public async Task Paste_WhenPutInAFutureSprint_ShouldLandAtItsTopInOrder()
    {
        var backlog = new StoriesBacklog(Page);
        var review = new PasteReview(Page);
        await Page.GotoAsync(Links.Backlog("SHIP", mode: "planning"));
        await Expect(backlog.Rows.First).ToBeVisibleAsync();

        await backlog.AddMany.ClickAsync();
        await review.FillAsync("Sprint paste one\nSprint paste two");
        await review.Place.SelectOptionAsync(new SelectOptionValue { Label = "Sprint 25" });
        var response = await review.CreateAsync();

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var section = await backlog.SectionAsync("Sprint 25");
        (section.Take(2)).Select(e => e.Title).ShouldBe(["Sprint paste one", "Sprint paste two"]);
        section.ShouldAllBe(e => e.Sprint == "Sprint 25");
        // Sprint 26 has no rows: the choice must also work for an empty sprint.
        await backlog.AddMany.ClickAsync();
        await review.FillAsync("Empty sprint one\nEmpty sprint two");
        await review.Place.SelectOptionAsync(new SelectOptionValue { Label = "Sprint 26" });
        (await review.CreateAsync()).Ok.ShouldBeTrue();
        (await backlog.SectionAsync("Sprint 26")).Select(e => e.Title).ShouldBe(["Empty sprint one", "Empty sprint two"]);
    }

    [Test]
    [Order(5)]
    public async Task Paste_WhenPutBelowTheLastRowOfASprintInPlanning_ShouldLandBelowItInTheSection()
    {
        var backlog = new StoriesBacklog(Page);
        var review = new PasteReview(Page);
        await Page.GotoAsync(Links.Backlog("SHIP", mode: "planning"));
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await FillingSteps.FocusRowAsync(backlog, "SHIP-147");
        (await backlog.SectionAsync("Sprint 25"))[^1].Key.ShouldBe("SHIP-147");

        await backlog.AddMany.ClickAsync();
        await review.FillAsync("Below paste one\nBelow paste two");
        await Expect(review.Place).ToHaveValueAsync("below");
        var response = await review.CreateAsync();

        response.Ok.ShouldBeTrue(await response.TextAsync());
        (await backlog.SectionAsync("Sprint 25")).TakeLast(2).Select(e => e.Title).ShouldBe(["Below paste one", "Below paste two"]);
    }

    // Same contract question as AddWhereYouAreTests: Planning sends `last`, which is the bottom of the whole backlog.
    [Test]
    [Order(6)]
    public async Task Paste_WhenBelowTheLastRowOfASprintInPlanning_ShouldSitBelowItInTheListToo()
    {
        var backlog = new StoriesBacklog(Page);
        var stack = await backlog.StackAsync();
        var at = stack.FindIndex(e => e.Key == "SHIP-147");

        stack.Skip(at + 1).Take(2).Select(e => e.Title).ShouldBe(["Below paste one", "Below paste two"]);
    }

    [Test]
    [Order(7)]
    public async Task Paste_WhenPutBelowARowInTheList_ShouldLandBetweenItAndTheNextSprint()
    {
        var backlog = new StoriesBacklog(Page);
        var review = new PasteReview(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await FillingSteps.FocusRowAsync(backlog, "SHIP-129");

        await backlog.AddMany.ClickAsync();
        await review.FillAsync("List below one\nList below two");
        var response = await review.CreateAsync();

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var stack = await backlog.StackAsync();
        var at = stack.FindIndex(e => e.Key == "SHIP-129");
        (stack.Skip(at + 1).Take(2)).Select(e => e.Title).ShouldBe(["List below one", "List below two"]);
        stack.Skip(at + 1).Take(2).ShouldAllBe(e => e.Sprint == "Sprint 24");
    }
}
