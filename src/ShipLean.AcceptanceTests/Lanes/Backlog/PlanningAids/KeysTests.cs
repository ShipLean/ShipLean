using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.PlanningAids;

// The list's keys, pressed on a real keyboard on the seeded Storefront project (SHIP) as Joel. Nothing here clicks a
// row or a checkbox to reach a command: Tab gets to the list and the keys do the rest. Each test changes rows the
// next ones don't read, except that Order(1) moves two rows to Sprint 26 and Order(5) adds a story.
[TestFixture]
public class KeysTests : SeededAcceptanceTest
{
    [Test]
    [Order(1)]
    public async Task Keys_WhenUsedOnList_ShouldSelectEstimateAndMove()
    {
        var backlog = new StoriesBacklog(Page);
        var keys = new BacklogKeys(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var shown = await keys.RowKeysAsync();
        shown.Count.ShouldBeGreaterThan(5);

        // Tab reaches the list on one row, the first, and not on a checkbox or a grip.
        await keys.TabToListAsync();
        (await keys.FocusedRowKeyAsync()).ShouldBe(shown[0]);

        // J and K walk the rows.
        await keys.PressAsync("j");
        await Expect(keys.Row(shown[1])).ToBeFocusedAsync();
        await keys.PressAsync("j");
        await Expect(keys.Row(shown[2])).ToBeFocusedAsync();
        await keys.PressAsync("k");
        await Expect(keys.Row(shown[1])).ToBeFocusedAsync();
        await keys.WalkToAsync(shown[0]);

        // X toggles every row (the row checkboxes are not tab stops, so this is the keyboard's only route to selecting).
        for (var n = 0; n < shown.Count; n++)
        {
            await keys.PressAsync("x");
            await Expect(keys.Row(shown[n])).ToHaveAttributeAsync("data-selected", "true");
            await keys.PressAsync("j");
        }

        await Expect(Page.GetByTestId(BacklogTestIds.BulkCount)).ToHaveTextAsync($"{shown.Count} selected");
        await keys.PressAsync("Escape");
        await Expect(Page.GetByTestId(BacklogTestIds.BulkBar)).ToHaveCountAsync(0);

        // Two rows selected by X, then M and a sprint's number moves them.
        await keys.WalkToAsync(shown[0]);
        await keys.PressAsync("x");
        await keys.PressAsync("j");
        await keys.PressAsync("x");
        await Expect(Page.GetByTestId(BacklogTestIds.BulkCount)).ToHaveTextAsync("2 selected");
        await keys.PressAsync("m");
        await Expect(Page.GetByTestId(BacklogTestIds.BulkMoveSprint).Nth(2)).ToContainTextAsync("Sprint 26");
        var moved = await Page.RunAndWaitForApiAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/backlog/bulk",
            "POST",
            () => keys.PressAsync("3"));
        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        var result = (await moved.JsonAsync()).GetValueOrDefault();
        result.GetProperty("written").GetArrayLength().ShouldBe(2);
        result.GetProperty("stale").GetArrayLength().ShouldBe(0);
        foreach (var key in shown.Take(2))
        {
            await Expect(keys.Row(key).GetByTestId(BacklogTestIds.SprintChip)).ToContainTextAsync("Sprint 26");
        }

        // A digit starts an estimate on the focused row (a row whose points are not 6) and Enter saves it.
        await keys.PressAsync("Escape");
        var target = shown.First(k => k != shown[0] && k != shown[1]);
        await keys.WalkToAsync(target);
        await keys.PressAsync("6");
        await Expect(Page.GetByTestId(BacklogTestIds.PointsInput)).ToHaveValueAsync("6");
        var item = await PlanningAidsApi.ItemAsync(Page.Context.APIRequest, target);
        var saved = await Page.RunAndWaitForResponseAsync(
            () => keys.PressAsync("Enter"),
            r => r.Request.Method == "PATCH" && r.Url.EndsWith($"/api/work-items/{item.GetProperty("id").GetGuid()}", StringComparison.Ordinal));
        saved.Ok.ShouldBeTrue(await saved.TextAsync());
        (await PlanningAidsApi.ItemAsync(Page.Context.APIRequest, target)).GetProperty("points").GetDecimal().ShouldBe(6m);
    }

    [Test]
    [Order(2)]
    public async Task Keys_WhenTypingOrInDialog_ShouldDoNothing()
    {
        var backlog = new StoriesBacklog(Page);
        var keys = new BacklogKeys(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var bar = Page.GetByTestId(BacklogTestIds.BulkBar);
        await keys.TabToListAsync();

        // In the search field every letter is text.
        await keys.PressAsync("/");
        await Expect(keys.SearchBox).ToBeFocusedAsync();
        await keys.TypeAsync("jxnm8?");
        await Expect(keys.SearchBox).ToHaveValueAsync("jxnm8?");
        await Expect(bar).ToHaveCountAsync(0);
        await Expect(backlog.AddRow).ToHaveCountAsync(0);
        await Expect(keys.Dialog).ToHaveCountAsync(0);
        await keys.PressAsync("Escape");
        await Expect(keys.SearchBox).ToHaveValueAsync(string.Empty);
        await Expect(backlog.Rows.First).ToBeFocusedAsync();

        // In the estimate field they are text too (or nothing), never commands.
        await keys.PressAsync("4");
        var points = Page.GetByTestId(BacklogTestIds.PointsInput);
        await Expect(points).ToBeFocusedAsync();
        await keys.PressAsync("x");
        await keys.PressAsync("j");
        await Expect(bar).ToHaveCountAsync(0);
        await Expect(points).ToBeVisibleAsync();
        await keys.PressAsync("Escape");
        await Expect(points).ToHaveCountAsync(0);

        // In the keys dialog they do nothing, and Esc closes it.
        await backlog.Rows.First.FocusAsync();
        await keys.PressAsync("?");
        await Expect(keys.Dialog).ToBeVisibleAsync();
        await keys.PressAsync("x");
        await keys.PressAsync("n");
        await keys.PressAsync("j");
        await Expect(bar).ToHaveCountAsync(0);
        await Expect(backlog.AddRow).ToHaveCountAsync(0);
        await keys.PressAsync("Escape");
        await Expect(keys.Dialog).ToHaveCountAsync(0);
        await Expect(bar).ToHaveCountAsync(0);
    }

    [Test]
    [Order(3)]
    public async Task Keys_WhenSlash_ShouldFocusSearch()
    {
        var backlog = new StoriesBacklog(Page);
        var keys = new BacklogKeys(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var all = await keys.RowKeysAsync();
        await keys.TabToListAsync();
        await keys.PressAsync("j");

        await keys.PressAsync("/");

        await Expect(keys.SearchBox).ToBeFocusedAsync();
        await keys.TypeAsync("passkey");
        await Expect(backlog.Rows).ToHaveCountAsync(1);
        await Expect(backlog.Rows.First).ToContainTextAsync("Sign in with passkey");
        await keys.PressAsync("Escape");
        await Expect(keys.SearchBox).ToHaveValueAsync(string.Empty);
        await Expect(backlog.Rows).ToHaveCountAsync(all.Count);
        (await keys.FocusedRowKeyAsync()).ShouldNotBeNull();
    }

    [Test]
    [Order(4)]
    public async Task Keys_WhenEnter_ShouldOpenModal()
    {
        var backlog = new StoriesBacklog(Page);
        var keys = new BacklogKeys(Page);
        var peek = new ItemPeekDialog(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var shown = await keys.RowKeysAsync();
        await keys.TabToListAsync();
        await keys.PressAsync("j");
        await Expect(keys.Row(shown[1])).ToBeFocusedAsync();

        await keys.PressAsync("Enter");

        await peek.WaitOpenAsync(shown[1]);
        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(Links.Backlog("SHIP"))}\\?item={shown[1]}$"));
        // Keys are the modal's, not the list's, while it is open.
        await keys.PressAsync("x");
        await Expect(Page.GetByTestId(BacklogTestIds.BulkBar)).ToHaveCountAsync(0);
        await peek.Close.ClickAsync();
        await peek.WaitClosedAsync();
        await Expect(Page.GetByTestId(BacklogTestIds.BulkBar)).ToHaveCountAsync(0);
    }

    [Test]
    [Order(5)]
    public async Task Keys_WhenN_ShouldAddBelow()
    {
        var backlog = new StoriesBacklog(Page);
        var keys = new BacklogKeys(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var shown = await keys.RowKeysAsync();
        await keys.TabToListAsync();
        await keys.PressAsync("j");
        await Expect(keys.Row(shown[1])).ToBeFocusedAsync();

        await keys.PressAsync("n");

        await Expect(backlog.AddRow).ToHaveAttributeAsync("data-anchor", shown[1]);
        await Expect(backlog.AddTitle).ToBeFocusedAsync();
        await keys.TypeAsync("Added with the N key");
        var added = await Page.RunAndWaitForApiAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/backlog/items",
            "POST",
            () => keys.PressAsync("Enter"));

        added.Ok.ShouldBeTrue(await added.TextAsync());
        var created = (await added.JsonAsync()).GetValueOrDefault().GetProperty("created");
        created.GetArrayLength().ShouldBe(1);
        // The new row sits directly below the focused one, here and after a reload.
        await Expect(backlog.Rows.Nth(2)).ToContainTextAsync("Added with the N key");
        (await keys.RowKeysAsync())[1].ShouldBe(shown[1]);
        await Page.ReloadAsync();
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await Expect(backlog.Rows.Nth(2)).ToContainTextAsync("Added with the N key");
        (await keys.RowKeysAsync())[1].ShouldBe(shown[1]);
    }

    [Test]
    [Order(6)]
    public async Task Keys_WhenQuestionMark_ShouldListKeys()
    {
        var backlog = new StoriesBacklog(Page);
        var keys = new BacklogKeys(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await keys.TabToListAsync();

        await keys.PressAsync("Shift+?");

        await Expect(keys.Dialog).ToBeVisibleAsync();
        await Expect(keys.Dialog).ToContainTextAsync("Move to a sprint");
        await keys.PressAsync("Escape");
        await Expect(keys.Dialog).ToHaveCountAsync(0);
    }

    [Test]
    [Order(7)]
    public async Task Keys_WhenUsedInPlanning_ShouldSkipClosedSectionsAndMove()
    {
        var planning = new PlanningBacklog(Page);
        var keys = new BacklogKeys(Page);
        var dragging = new PlanningDragging(Page);
        await planning.OpenAsync("SHIP");
        await Expect(keys.Row("SHIP-156")).ToBeVisibleAsync();

        // The running sprint starts closed, so J goes from the first row of Sprint 25 over to the Backlog's.
        await Expect(keys.Row("SHIP-129")).ToHaveCountAsync(0);
        await keys.TabToListAsync();
        var shown = await keys.RowKeysAsync();
        (await keys.FocusedRowKeyAsync()).ShouldBe(shown[0]);
        await keys.WalkToAsync("SHIP-156");

        await keys.PressAsync("x");
        await keys.PressAsync("j");
        await keys.PressAsync("x");
        await Expect(Page.GetByTestId(BacklogTestIds.BulkCount)).ToHaveTextAsync("2 selected");
        await keys.PressAsync("m");
        var moved = await Page.RunAndWaitForApiAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/backlog/bulk",
            "POST",
            () => keys.PressAsync("2"));

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        (await moved.JsonAsync()).GetValueOrDefault().GetProperty("written").GetArrayLength().ShouldBe(2);
        var sprint25 = SeedIds.Sprints.Sprint25.ToString();
        await Expect(keys.Row("SHIP-156")).ToHaveAttributeAsync("data-section", sprint25);
        (await dragging.SectionOfAsync("SHIP-158")).ShouldBe(sprint25);
    }
}
