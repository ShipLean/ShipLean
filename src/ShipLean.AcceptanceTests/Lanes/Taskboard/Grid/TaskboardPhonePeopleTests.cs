using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.Grid;

// The People grouping and a teammate's move on the sprint Taskboard on a phone (SHIP-725 D16, D18): sprint 24 of the seeded
// Storefront project, signed in as Joel.
[TestFixture]
public class TaskboardPhonePeopleTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> ClientHeader = new() { ["X-ShipLean-Client"] = "taskboard-phone-people-test" };

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private async Task<PhoneGridPart> OpenAsync()
    {
        var grid = new PhoneGridPart(Page);
        await Page.GotoAsync(Links.Taskboard("SHIP", SeedIds.Sprints.Sprint24));
        await grid.WaitAsync();
        return grid;
    }

    private async Task SaveGroupingAsync(string groupBy)
    {
        var saved = await Page.Context.APIRequest.PutAsync("/api/me/preferences/view:taskboard", new APIRequestContextOptions { DataObject = new { groupBy }, Headers = ClientHeader });
        saved.Status.ShouldBe(204);
    }

    [Test]
    public async Task Taskboard_WhenGroupedByPeopleOnPhone_ShouldShowEveryoneWithUnassignedFirst()
    {
        var members = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/members");
        members.Ok.ShouldBeTrue(await members.TextAsync());
        var names = JsonDocument.Parse(await members.TextAsync()).RootElement.EnumerateArray()
            .Select(member => member.GetProperty("user").GetProperty("name").GetString()!)
            .ToList();
        await SaveGroupingAsync("people");
        try
        {
            var grid = await OpenAsync();

            var count = await grid.Pages.CountAsync();
            count.ShouldBeGreaterThan(1);
            for (var i = 0; i < count; i++)
            {
                var titles = await PhoneGridPart.LaneTitlesAsync(grid.Pages.Nth(i));
                titles[0].ShouldBe("Unassigned");
                titles.Count.ShouldBe(names.Count + 1);
                titles.ShouldBeSubsetOf(names.Append("Unassigned"));
            }
            (await TouchRules.ViolationsAsync(Page, grid.Chips.First)).ShouldBeEmpty();
            (await TouchRules.ViolationsAsync(Page, PhoneGridPart.Lanes(grid.Pages.First).First.GetByTestId(TaskboardTestIds.RowHeader))).ShouldBeEmpty();
        }
        finally
        {
            await SaveGroupingAsync("stories");
        }
    }

    [Test]
    public async Task Taskboard_WhenTeammateMovesTaskOnPhone_ShouldPulseTargetChipAndStayOnPage()
    {
        var grid = await OpenAsync();
        await Expect(PhoneGridPart.PageHead(grid.Pages.First)).ToBeInViewportAsync(new LocatorAssertionsToBeInViewportOptions { Ratio = 1 });
        await Expect(grid.Chip("In review")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("^(?!.*is-pulsing)"));

        await using var tom = await TaskboardTeammate.SignInAsTomAsync(Page, BaseUrl);
        await tom.MoveToStateAsync(SeedIds.Sprints.Sprint24, "SHIP-178", SeedIds.States.Ship.TaskInReview);

        await Expect(grid.Chip("In review")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-pulsing"));
        await Expect(PhoneGridPart.PageHead(grid.Pages.First)).ToBeInViewportAsync(new LocatorAssertionsToBeInViewportOptions { Ratio = 1 });
        await Expect(grid.Chips.First).ToHaveAttributeAsync("aria-current", "true");
    }
}
