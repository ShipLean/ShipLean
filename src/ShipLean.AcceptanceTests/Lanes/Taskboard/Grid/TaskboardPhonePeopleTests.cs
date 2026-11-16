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

    // Moves a task to a person's row in its own state (null = Unassigned), against the revision read now.
    private async Task AssignAsync(Guid taskId, Guid stateId, Guid? person)
    {
        var read = await Page.Context.APIRequest.GetAsync($"/api/sprints/{SeedIds.Sprints.Sprint24}/taskboard");
        read.Ok.ShouldBeTrue();
        using var view = JsonDocument.Parse(await read.TextAsync());
        var card = view.RootElement.GetProperty("peopleRows").EnumerateArray()
            .SelectMany(row => row.GetProperty("cells").EnumerateArray())
            .SelectMany(cell => cell.GetProperty("tasks").EnumerateArray())
            .First(task => task.GetProperty("id").GetGuid() == taskId);
        var moved = await Page.Context.APIRequest.PostAsync($"/api/sprints/{SeedIds.Sprints.Sprint24}/taskboard/moves", new APIRequestContextOptions
        {
            Headers = ClientHeader,
            DataObject = new
            {
                taskId,
                expectedRevision = card.GetProperty("revision").GetInt32(),
                stateId,
                row = new { grouping = "people", target = person },
                beforeItemId = (Guid?)null,
            },
        });
        moved.Ok.ShouldBeTrue(await moved.TextAsync());
    }

    [Test]
    public async Task Taskboard_WhenGroupedByPeopleOnPhone_ShouldShowEveryoneWithUnassignedFirst()
    {
        var members = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/members");
        members.Ok.ShouldBeTrue(await members.TextAsync());
        var names = JsonDocument.Parse(await members.TextAsync()).RootElement.EnumerateArray()
            .Select(member => member.GetProperty("user").GetProperty("name").GetString()!)
            .ToList();
        var read = await Page.Context.APIRequest.GetAsync($"/api/sprints/{SeedIds.Sprints.Sprint24}/taskboard");
        using var board = JsonDocument.Parse(await read.TextAsync());
        // The member with the fewest tasks is left with none for this test: their tasks go to Unassigned and come back after.
        var leaving = board.RootElement.GetProperty("peopleRows").EnumerateArray()
            .Where(row => row.GetProperty("person").ValueKind == JsonValueKind.Object)
            .OrderBy(row => row.GetProperty("taskCount").GetInt32())
            .First();
        var person = leaving.GetProperty("person");
        var tasks = leaving.GetProperty("cells").EnumerateArray()
            .SelectMany(cell => cell.GetProperty("tasks").EnumerateArray().Select(task => (Id: task.GetProperty("id").GetGuid(), State: cell.GetProperty("stateId").GetGuid())))
            .ToList();
        var personName = person.GetProperty("name").GetString()!;
        var personId = person.GetProperty("id").GetGuid();
        await SaveGroupingAsync("people");
        foreach (var task in tasks)
        {
            await AssignAsync(task.Id, task.State, null);
        }

        try
        {
            var grid = await OpenAsync();

            var count = await grid.Pages.CountAsync();
            count.ShouldBeGreaterThan(1);
            for (var i = 0; i < count; i++)
            {
                var statePage = grid.Pages.Nth(i);
                var titles = await PhoneGridPart.LaneTitlesAsync(statePage);
                titles[0].ShouldBe("Unassigned");
                titles.Count.ShouldBe(names.Count + 1);
                titles.ShouldBeSubsetOf(names.Append("Unassigned"));
                await Expect(grid.LaneTitled(statePage, personName).GetByTestId(TaskboardTestIds.PhonePersonEmpty)).ToHaveTextAsync("No tasks in this sprint");
            }

            (await TouchRules.ViolationsAsync(Page, grid.Chips.First)).ShouldBeEmpty();
            var empty = grid.LaneTitled(grid.Pages.First, personName);
            (await TouchRules.ViolationsAsync(Page, empty.GetByTestId(TaskboardTestIds.RowHeader))).ShouldBeEmpty();
        }
        finally
        {
            foreach (var task in tasks)
            {
                await AssignAsync(task.Id, task.State, personId);
            }

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
