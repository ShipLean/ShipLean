using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.Grid;

// The extras of the sprint Taskboard's lanes on a phone (SHIP-725 D12, D14): sprint 24 of the seeded Storefront project,
// signed in as Joel. The tests run in order: the offer test closes SHIP-176, the last open task of Address autocomplete.
[TestFixture]
public class TaskboardPhoneLanesTests : SeededAcceptanceTest
{
    private const string AddressAutocomplete = "Address autocomplete";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private async Task<PhoneGridPart> OpenAsync()
    {
        var grid = new PhoneGridPart(Page);
        await Page.GotoAsync(Links.Taskboard("SHIP", SeedIds.Sprints.Sprint24));
        await grid.WaitAsync();
        return grid;
    }

    [Test]
    [Order(1)]
    public async Task Taskboard_WhenOpenedOnPhone_ShouldShowUnparentedLastOnEveryPage()
    {
        var grid = await OpenAsync();

        var count = await grid.Pages.CountAsync();
        count.ShouldBeGreaterThan(1);
        for (var i = 0; i < count; i++)
        {
            var lanes = PhoneGridPart.Lanes(grid.Pages.Nth(i));
            await Expect(lanes.Last).ToHaveAttributeAsync("data-row", "unparented");
            await Expect(lanes.Last.GetByTestId(TaskboardTestIds.LaneTasks)).ToHaveAttributeAsync("data-drop-target", "");
            await Expect(lanes.Last.GetByTestId(TaskboardTestIds.RowCount)).ToContainTextAsync("no points");
        }
    }

    [Test]
    [Order(2)]
    public async Task Taskboard_WhenLastTaskOfStoryDoneOnPhone_ShouldOfferMoveToResolvedOnEveryPage()
    {
        await OpenAsync();
        await BacklogApi.SetStateAsync(Page, "SHIP-176", SeedIds.States.Ship.TaskClosed);
        var grid = await OpenAsync();

        await Expect(grid.Pages.First.GetByTestId(TaskboardTestIds.RowOffer)).ToContainTextAsync("Move to Resolved");
        await grid.TurnToAsync("Closed");
        var last = grid.PageNamed("Closed");
        var offer = grid.LaneTitled(last, AddressAutocomplete).GetByTestId(TaskboardTestIds.RowOffer);
        await Expect(offer).ToBeVisibleAsync();
        var resolve = offer.GetByTestId(TaskboardTestIds.RowOfferResolve);
        (await TouchRules.ViolationsAsync(Page, offer)).ShouldBeEmpty();

        var moved = await Page.RunAndWaitForResponseAsync(
            () => resolve.TapAsync(),
            r => r.Request.Method == "PATCH" && new Uri(r.Url).AbsolutePath.StartsWith("/api/work-items/", StringComparison.Ordinal));

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        var written = JsonDocument.Parse(await moved.TextAsync()).RootElement;
        var story = await BacklogApi.ReadAsync(Page, $"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{written.GetProperty("key").GetString()}");
        story.GetProperty("state").GetProperty("category").GetString().ShouldBe("resolved");
        await Expect(offer).ToHaveCountAsync(0);
    }
}
