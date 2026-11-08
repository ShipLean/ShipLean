using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.FeatureBoard;

// Checkout v2 (SHIP-101) on a phone (SHIP-722): the shared Taskboard grid as a state pager, one task state a page, every
// open story a lane on every page. Signed in as Joel on the seeded Storefront project. No test changes work items.
[TestFixture]
public class FeatureBoardPhoneTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string Feature = "SHIP-101";
    private const string PaySavedCard = "Pay with saved card";
    private const string GiftCard = "Gift card balance check";
    private const string InReview = "In review";
    private static readonly Dictionary<string, string> ClientHeader = new() { ["X-ShipLean-Client"] = "feature-board-phone-test" };

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    [Order(1)]
    public async Task FeatureBoard_WhenOpenedOnPhone_ShouldShowOneStatePageWithEveryOpenStoryAsLane()
    {
        var grid = new PhoneGridPart(Page);
        var view = await ViewAsync();
        var (openStories, closedStories) = Stories(view);

        await Page.GotoAsync(Links.FeatureBoard(Project, Feature));
        await grid.WaitAsync();

        await Expect(Page.GetByTestId(TaskboardTestIds.Grid)).ToHaveCountAsync(0);
        var pages = await grid.Pages.CountAsync();
        pages.ShouldBeGreaterThan(1);
        await Expect(grid.Chips).ToHaveCountAsync(pages);
        await Expect(grid.Chips.First).ToHaveAttributeAsync("aria-current", "true");
        await Expect(grid.Chips.Nth(1)).Not.ToHaveAttributeAsync("aria-current", "true");
        await Expect(PhoneGridPart.PageHead(grid.Pages.First)).ToBeInViewportAsync(new() { Ratio = 1 });
        var first = await PhoneGridPart.LaneTitlesAsync(grid.Pages.First);
        first.ShouldBe(openStories);
        first.ShouldContain(PaySavedCard);
        first.ShouldContain(GiftCard);
        for (var i = 1; i < pages; i++)
        {
            (await PhoneGridPart.LaneTitlesAsync(grid.Pages.Nth(i))).ShouldBe(first);
        }
        (await PhoneGridPart.ClosedLines(grid.Pages.First).GetByTestId(TaskboardTestIds.RowTitle).AllInnerTextsAsync()).ShouldBe(closedStories);
        (await TouchRules.ViolationsAsync(Page, grid.ChipStrip)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, PhoneGridPart.Lanes(grid.Pages.First).First.GetByTestId(TaskboardTestIds.RowHeader))).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, PhoneGridPart.ClosedLines(grid.Pages.First).First)).ShouldBeEmpty();
    }

    [Test]
    [Order(2)]
    public async Task FeatureBoard_WhenStateChipTappedOnPhone_ShouldTurnToThatStateAndShowNoneInEmptyLanes()
    {
        var grid = new PhoneGridPart(Page);
        await Page.GotoAsync(Links.FeatureBoard(Project, Feature));
        await grid.WaitAsync();
        var inReview = grid.PageNamed(InReview);

        await grid.TurnToAsync(InReview);

        await Expect(grid.Chip(InReview)).ToHaveAttributeAsync("aria-current", "true");
        await Expect(PhoneGridPart.PageHead(inReview)).ToBeInViewportAsync(new() { Ratio = 1 });
        var lanes = PhoneGridPart.Lanes(inReview);
        var count = await lanes.CountAsync();
        var none = 0;
        for (var i = 0; i < count; i++)
        {
            var lane = lanes.Nth(i);
            var cards = await lane.GetByTestId(TaskboardTestIds.Card).CountAsync();
            var empty = await lane.GetByTestId(TaskboardTestIds.LaneNone).CountAsync();
            (cards > 0 ^ empty == 1).ShouldBeTrue($"lane {i}: {cards} cards, {empty} None");
            none += empty;
        }
        none.ShouldBeGreaterThan(0);
        await Expect(grid.LaneTitled(inReview, GiftCard).GetByTestId(TaskboardTestIds.LaneTasks)).ToHaveAttributeAsync("data-drop-target", "");
    }

    [Test]
    [Order(3)]
    public async Task FeatureBoard_WhenGroupedByPeopleOnPhone_ShouldShowPeopleAsLanes()
    {
        var grid = new PhoneGridPart(Page);
        var people = People(await ViewAsync());
        await SaveGroupingAsync("people");
        try
        {
            await Page.GotoAsync(Links.FeatureBoard(Project, Feature));
            await grid.WaitAsync();

            var first = grid.Pages.First;
            await Expect(PhoneGridPart.Lanes(first).First).ToHaveAttributeAsync("data-lane", "person");
            var titles = await PhoneGridPart.LaneTitlesAsync(first);
            titles.ShouldBe(people);
            titles.ShouldNotContain(PaySavedCard);
            for (var i = 1; i < await grid.Pages.CountAsync(); i++)
            {
                (await PhoneGridPart.LaneTitlesAsync(grid.Pages.Nth(i))).ShouldBe(titles);
            }
            var card = grid.Grid.GetByTestId(TaskboardTestIds.Card).First;
            await Expect(card.GetByTestId(TaskboardTestIds.CardParent)).ToBeVisibleAsync();
            (await TouchRules.ViolationsAsync(Page, PhoneGridPart.Lanes(first).First.GetByTestId(TaskboardTestIds.RowHeader))).ShouldBeEmpty();
        }
        finally
        {
            await SaveGroupingAsync("stories");
        }
    }

    private async Task SaveGroupingAsync(string groupBy)
    {
        var saved = await Page.Context.APIRequest.PutAsync("/api/me/preferences/view:taskboard", new APIRequestContextOptions { DataObject = new { groupBy }, Headers = ClientHeader });
        saved.Status.ShouldBe(204);
    }

    private async Task<JsonElement> ViewAsync()
    {
        var response = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{Feature}/feature-board");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    // The people rows' names, in the view's order (Unassigned first when present).
    private static List<string> People(JsonElement view) =>
        view.GetProperty("peopleRows").EnumerateArray()
            .Select(row => row.GetProperty("person") is { ValueKind: JsonValueKind.Object } person ? person.GetProperty("name").GetString()! : "Unassigned")
            .ToList();

    // The Feature's story titles from its board view, in backlog order: open (and Unparented), and closed.
    private static (List<string> Open, List<string> Closed) Stories(JsonElement view)
    {
        var open = new List<string>();
        var closed = new List<string>();
        foreach (var row in view.GetProperty("rows").EnumerateArray())
        {
            var parent = row.GetProperty("parent");
            if (parent.ValueKind == JsonValueKind.Null)
            {
                open.Add("Unparented");
            }
            else if (parent.GetProperty("state").GetProperty("category").GetString() == "completed")
            {
                closed.Add(parent.GetProperty("title").GetString()!);
            }
            else
            {
                open.Add(parent.GetProperty("title").GetString()!);
            }
        }
        return (open, closed);
    }
}
