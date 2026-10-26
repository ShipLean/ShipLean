using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Import.Run;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.TestSupport.Jira;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Planning;

// PLATFORM's issues imported from the stub Jira into a new project, then planned into a sprint and found on the Stories board.
[TestFixture]
public class ImportThenPlanJourneyTests : SeededAcceptanceTest
{
    private const string ProjectKey = "PLAT";

    private readonly JiraStubHandler _stub = new();
    private readonly AttachmentGate _gate = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => ImportRunArrangement.PinJira(services, _stub, _gate);

    [Test]
    public async Task J2_JiraImport_ThenPlannedOnBoard()
    {
        // 1. The import, from the project-less entry to a finished run.
        await ImportRunArrangement.WalkToRunAsync(Page);
        var run = new ImportRunPage(Page);
        await run.StartAsync();
        await run.ExpectFinishedAsync("done");
        await Expect(run.IssueCount).ToContainTextAsync("10 / 10");

        // 2. The imported stories keep their Jira numbers in their keys.
        var backlog = new StoriesBacklog(Page);
        var dragging = new PlanningDragging(Page);
        await backlog.OpenAsync(ProjectKey);
        var keys = new[] { "PLAT-411", "PLAT-412" };
        foreach (var key in keys)
        {
            await Expect(dragging.Row(key)).ToHaveCountAsync(1);
        }

        // 3. Planning: a new sprint, then the two stories into it.
        var planning = new PlanningBacklog(Page);
        await dragging.ShowWholePageAsync();
        await planning.OpenAsync(ProjectKey);
        var created = await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId(BacklogTestIds.PlanningCreateSprint).ClickAsync(),
            r => r.Request.Method == "POST" && new Uri(r.Url).AbsolutePath.EndsWith("/sprints", StringComparison.Ordinal));
        created.Ok.ShouldBeTrue(await created.TextAsync());
        var sprintName = (await created.JsonAsync())!.Value.GetProperty("name").GetString()!;
        var sprintId = (await created.JsonAsync())!.Value.GetProperty("id").GetGuid().ToString();
        sprintName.ShouldBe("Sprint 14");
        await Expect(planning.Section(sprintName)).ToBeVisibleAsync();
        foreach (var key in keys)
        {
            var moved = await dragging.DragOntoHeaderAsync(key, sprintName);
            moved.Ok.ShouldBeTrue(await moved.TextAsync());
            await Expect(dragging.Toasts.Filter(new() { HasText = $"{key} → {sprintName}" })).ToBeVisibleAsync();
            await Expect(dragging.Row(key)).ToHaveAttributeAsync("data-section", sprintId);
        }

        // 4. The Stories board shows both cards.
        var board = new BoardScreen(Page);
        await board.OpenAsync(ProjectKey, "stories");
        await new BoardFilter(Page).Toggle.ClickAsync();
        var sprintFilter = Page.GetByTestId(BoardTestIds.FilterSprint);
        await sprintFilter.SelectOptionAsync(new SelectOptionValue { Label = sprintName });
        foreach (var key in keys)
        {
            await Expect(board.Card(key)).ToBeVisibleAsync();
        }

        await Expect(board.Card("PLAT-405")).ToHaveCountAsync(0);
    }
}
