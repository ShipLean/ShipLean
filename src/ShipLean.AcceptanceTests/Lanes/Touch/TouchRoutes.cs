using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Touch;

// Every SPA route (ScreenIds.All) as the phone route sweep opens it, on the seed. A new route adds one line here, or
// TouchRoutes_ShouldCoverEveryScreen fails. A swept route's ready ids render only once its data has (a seeded row, card
// or the item's key; where the seed has nothing, an element its loaded query renders), one per query the screen waits
// on, never the page wrapper, which shows before the data and would let the sweep pass a skeleton. The exempt routes
// are not swept and keep their page wrappers.
public static class TouchRoutes
{
    private const string Project = "SHIP";

    public static readonly IReadOnlyDictionary<string, TouchRoute> All = new Dictionary<string, TouchRoute>(StringComparer.Ordinal)
    {
        ["login"] = new(_ => "/login", [AuthTestIds.SignInForm], SignedOut: true),
        ["newProject"] = new(_ => Links.NewProject(), [PagesTestIds.NewProjectPage]),
        ["instanceSettings"] = new(_ => Links.InstanceSettings("general"), [PagesTestIds.InstanceSettingsPage]),
        ["profile"] = new(_ => Links.Profile(), [SettingsTestIds.MyProjectRow]),
        ["importStart"] = new(_ => Links.JiraImportStart(), [PagesTestIds.ImportPage]),
        ["storiesBacklog"] = new(_ => Links.Backlog(Project, "stories"), [BacklogTestIds.Row]),
        ["featuresBacklog"] = new(_ => Links.Backlog(Project, "features"), [BacklogTestIds.Row]),
        ["epicsBacklog"] = new(_ => Links.Backlog(Project, "epics"), [BacklogTestIds.Row]),
        ["workItem"] = new(_ => Links.WorkItem(Project, "SHIP-141"), [WorkItemTestIds.Key]),
        ["workItemAttachment"] = new(
            attachment => $"{Links.WorkItem(Project, "SHIP-101")}/attachments/{attachment}", [ComponentTestIds.Markdown]),
        ["featureBoard"] = new(_ => Links.FeatureBoard(Project, "SHIP-101"), [TaskboardTestIds.Card]),
        ["storiesBoard"] = new(_ => Links.Board(Project, "stories"), [BoardTestIds.Card]),
        ["featuresBoard"] = new(_ => Links.Board(Project, "features"), [BoardTestIds.Card]),
        ["epicsBoard"] = new(_ => Links.Board(Project, "epics"), [BoardTestIds.Card]),
        ["sprints"] = new(_ => Links.Sprints(Project), [SprintsTestIds.ListRow, SprintsTestIds.NewSprintName]),
        ["sprintBacklog"] = new(
            _ => Links.SprintBacklog(Project, SeedIds.Sprints.Sprint24), [SprintsTestIds.StoryRow, SprintsTestIds.KpiCapacityEdit]),
        ["taskboard"] = new(_ => Links.Taskboard(Project, SeedIds.Sprints.Sprint24), [TaskboardTestIds.Card]),
        ["capacity"] = new(_ => Links.SprintCapacity(Project, SeedIds.Sprints.Sprint24), [SprintsTestIds.CapacityRow]),
        ["sprintRetro"] = new(_ => Links.SprintRetro(Project, SeedIds.Sprints.Sprint23), [RetrosTestIds.RetroNumbers]),
        ["sprintDemo"] = new(_ => Links.SprintDemo(Project, SeedIds.Sprints.Sprint24), [DemosTestIds.DemoStatusMessage]),
        ["retros"] = new(_ => Links.Retros(Project), [RetrosTestIds.RetrosSearchPanel]),
        ["retroFeatures"] = new(_ => Links.RetroFeatures(Project), [RetrosTestIds.RetroFeaturesPanel]),
        ["retroItem"] = new(_ => Links.RetroItem(Project, "SHIP-101"), [RetrosTestIds.RetroItemChildRow]),
        ["retroInsights"] = new(_ => Links.RetroInsights(Project), [RetrosTestIds.RetroRecurringTable]),
        ["jiraImport"] = new(_ => Links.JiraImport(Project), [PagesTestIds.ImportPage]),
        ["projectSettings"] = new(_ => Links.ProjectSettings(Project, "general"), [SettingsTestIds.GeneralName]),
        ["authorize"] = new(_ => "/authorize", [AgentsTestIds.AuthorizePage]),
    };

    // Opens a route and checks it against the touch rules: a route in TouchPendingRoutes must still break them (so a
    // finished route can't stay listed), any other route must follow them.
    public static async Task CheckAsync(IPage page, string screenId, Guid markdownAttachment)
    {
        var route = All[screenId];
        await page.GotoAsync(route.Url(markdownAttachment));
        foreach (var readyTestId in route.ReadyTestIds)
        {
            await Assertions.Expect(page.GetByTestId(readyTestId).First).ToBeVisibleAsync();
        }

        var violations = await TouchRules.ViolationsAsync(page);

        if (TouchPendingRoutes.All.ContainsKey(screenId))
        {
            violations.ShouldNotBeEmpty($"{screenId} passes on a phone: delete its line from TouchPendingRoutes");
        }
        else
        {
            violations.ShouldBeEmpty($"{screenId} breaks the touch rules on a phone:\n{string.Join("\n", violations)}");
        }
    }
}
