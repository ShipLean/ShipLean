using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Backlog.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Touch;

// Planning on a phone (SHIP-716 D2, D5), on the seeded Storefront project (SHIP). Both tests move items. Neither reads the whole
// screen: the touch rules are checked on the sprint blocks and the pull line, the regions this lane builds.
[TestFixture]
public class PlanningTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    // The keys of a section's rows, top to bottom (the rows sit in a block carrying the section's id).
    private async Task<List<string>> KeysInAsync(string sectionId) =>
        [.. await Page.GetByTestId(BacklogTestIds.PlanningTouchRows).EvaluateAllAsync<string[]>(
            "(blocks, id) => blocks.filter(b => b.getAttribute('data-section') === id).flatMap(b => [...b.querySelectorAll('[data-key]')].map(r => r.getAttribute('data-key') ?? ''))",
            sectionId)];

    // Waits until a section's rows include `key`.
    private Task WaitForRowInAsync(string sectionId, string key) =>
        Page.WaitForFunctionAsync(
            "([testId, id, key]) => [...document.querySelectorAll(`[data-testid='${testId}'][data-section='${id}'] [data-key]`)].some(r => r.getAttribute('data-key') === key)",
            new object[] { BacklogTestIds.PlanningTouchRows, sectionId, key });

    private ILocator RowOf(string key) => Page.GetByTestId(BacklogTestIds.Row).Filter(new() { Has = Page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = new($"^{key}$") }) });

    [Test]
    public async Task Row_WhenDroppedOnSprintHeaderOnPhone_ShouldMoveToThatSprint()
    {
        var planning = new PlanningBacklog(Page);
        await planning.OpenAsync("SHIP");
        // Fold the two sprints above, so the Backlog's first row and Sprint 26's header are on screen together.
        foreach (var title in new[] { "Sprint 24", "Sprint 25" })
        {
            await planning.Section(title).GetByTestId(BacklogTestIds.SectionToggle).TapAsync();
        }

        var sprint26 = SeedIds.Sprints.Sprint26.ToString();
        var moved = (await KeysInAsync("backlog"))[0];
        var rank = await RowOf(moved).GetByTestId(BacklogTestIds.Rank).InnerTextAsync();
        var reorders = new List<string>();
        Page.Request += (_, request) =>
        {
            if (request.Url.EndsWith("/reorder", StringComparison.Ordinal))
            {
                reorders.Add(request.Url);
            }
        };

        var patched = Page.RunAndWaitForResponseAsync(
            () => TouchDrag.ByHandleAsync(Page, RowOf(moved).GetByTestId(BacklogTestIds.TouchHandle), planning.Section("Sprint 26")),
            r => r.Request.Method == "PATCH" && r.Url.Contains("/api/work-items/", StringComparison.Ordinal));

        (await patched).Ok.ShouldBeTrue();
        await WaitForRowInAsync(sprint26, moved);
        (await KeysInAsync("backlog")).ShouldNotContain(moved);
        (await RowOf(moved).GetByTestId(BacklogTestIds.Rank).InnerTextAsync()).ShouldBe(rank);

        await Page.ReloadAsync();
        await Expect(planning.Section("Backlog")).ToBeVisibleAsync();
        await WaitForRowInAsync(sprint26, moved);
        (await KeysInAsync(sprint26)).ShouldContain(moved);
        (await RowOf(moved).GetByTestId(BacklogTestIds.Rank).InnerTextAsync()).ShouldBe(rank);
        reorders.ShouldBeEmpty();
        foreach (var block in await planning.Sections.AllAsync())
        {
            (await TouchRules.ViolationsAsync(Page, block)).ShouldBeEmpty();
        }
    }

    [Test]
    public async Task PullLine_WhenMovedDownAndPulledOnPhone_ShouldPullThoseItems()
    {
        var planning = new PlanningBacklog(Page);
        var pull = new PullLine(Page);
        await planning.OpenAsync("SHIP");
        await Expect(pull.Line).ToBeVisibleAsync();
        await pull.Line.ScrollIntoViewIfNeededAsync();
        var before = await KeysAboveAsync(pull);
        before.ShouldNotBeEmpty();
        var backlogKeys = await KeysInAsync("backlog");
        var expected = backlogKeys.Take(before.Count + 1).ToList();
        expected.Count.ShouldBe(before.Count + 1, "the seeded Backlog has a row below the line");

        await Page.GetByTestId(BacklogTestIds.PullDown).TapAsync();
        await Expect(pull.Content).ToContainTextAsync($"{expected.Count} items");
        (await KeysAboveAsync(pull)).ShouldBe(expected);
        (await TouchRules.ViolationsAsync(Page, pull.Line)).ShouldBeEmpty();

        var response = await Page.RunAndWaitForResponseAsync(
            () => pull.Go.TapAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/backlog/bulk", StringComparison.Ordinal));

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var items = JsonDocument.Parse(response.Request.PostData!).RootElement.GetProperty("items").GetArrayLength();
        items.ShouldBe(expected.Count);

        await Page.ReloadAsync();
        await Expect(planning.Section("Backlog")).ToBeVisibleAsync();
        var sprint25 = await KeysInAsync(SeedIds.Sprints.Sprint25.ToString());
        expected.ForEach(key => sprint25.ShouldContain(key));
        (await KeysInAsync("backlog")).ShouldNotContain(expected[0]);
    }

    // The keys of the Backlog's rows above the pull line, top to bottom.
    private async Task<List<string>> KeysAboveAsync(PullLine pull)
    {
        var line = await pull.Line.ElementHandleAsync();
        var keys = await Page.GetByTestId(BacklogTestIds.Row).EvaluateAllAsync<string[]>(
            "(rows, line) => rows.filter(r => r.closest('[data-section=backlog]') !== null && (line.compareDocumentPosition(r) & Node.DOCUMENT_POSITION_PRECEDING) !== 0).map(r => r.getAttribute('data-key') ?? '')",
            line);
        return [.. keys];
    }
}
