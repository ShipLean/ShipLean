using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Touch;

// Every SPA route (ScreenIds.All) as the phone route sweep opens it, on the seed. A new route adds one line here, or
// TouchRoutes_ShouldCoverEveryScreen fails.
public static class TouchRoutes
{
    private const string Project = "SHIP";

    public static readonly IReadOnlyDictionary<string, TouchRoute> All = new Dictionary<string, TouchRoute>(StringComparer.Ordinal)
    {
        ["login"] = new(_ => "/login", AuthTestIds.SignInForm, SignedOut: true),
        ["newProject"] = new(_ => Links.NewProject(), PagesTestIds.NewProjectPage),
        ["instanceSettings"] = new(_ => Links.InstanceSettings("general"), PagesTestIds.InstanceSettingsPage),
        ["profile"] = new(_ => Links.Profile(), PagesTestIds.ProfilePage),
        ["importStart"] = new(_ => Links.JiraImportStart(), PagesTestIds.ImportPage),
        ["storiesBacklog"] = new(_ => Links.Backlog(Project, "stories"), PagesTestIds.BacklogPage),
        ["featuresBacklog"] = new(_ => Links.Backlog(Project, "features"), PagesTestIds.BacklogPage),
        ["epicsBacklog"] = new(_ => Links.Backlog(Project, "epics"), PagesTestIds.BacklogPage),
        ["workItem"] = new(_ => Links.WorkItem(Project, "SHIP-141"), PagesTestIds.WorkItemPage),
        ["workItemAttachment"] = new(
            attachment => $"{Links.WorkItem(Project, "SHIP-101")}/attachments/{attachment}", PagesTestIds.AttachmentPage),
        ["featureBoard"] = new(_ => Links.FeatureBoard(Project, "SHIP-101"), PagesTestIds.FeatureBoardPage),
        ["storiesBoard"] = new(_ => Links.Board(Project, "stories"), PagesTestIds.BoardPage),
        ["featuresBoard"] = new(_ => Links.Board(Project, "features"), PagesTestIds.BoardPage),
        ["epicsBoard"] = new(_ => Links.Board(Project, "epics"), PagesTestIds.BoardPage),
        ["sprints"] = new(_ => Links.Sprints(Project), PagesTestIds.SprintsPage),
        ["sprintBacklog"] = new(_ => Links.SprintBacklog(Project, SeedIds.Sprints.Sprint24), PagesTestIds.SprintBacklogPage),
        ["taskboard"] = new(_ => Links.Taskboard(Project, SeedIds.Sprints.Sprint24), PagesTestIds.TaskboardPage),
        ["capacity"] = new(_ => Links.SprintCapacity(Project, SeedIds.Sprints.Sprint24), PagesTestIds.CapacityPage),
        ["sprintRetro"] = new(_ => Links.SprintRetro(Project, SeedIds.Sprints.Sprint23), PagesTestIds.SprintRetroPage),
        ["sprintDemo"] = new(_ => Links.SprintDemo(Project, SeedIds.Sprints.Sprint24), PagesTestIds.SprintDemoPage),
        ["retros"] = new(_ => Links.Retros(Project), PagesTestIds.RetrosPage),
        ["retroFeatures"] = new(_ => Links.RetroFeatures(Project), PagesTestIds.RetroFeaturesPage),
        ["retroItem"] = new(_ => Links.RetroItem(Project, "SHIP-101"), PagesTestIds.RetroItemPage),
        ["retroInsights"] = new(_ => Links.RetroInsights(Project), PagesTestIds.RetroInsightsPage),
        ["jiraImport"] = new(_ => Links.JiraImport(Project), PagesTestIds.ImportPage),
        ["projectSettings"] = new(_ => Links.ProjectSettings(Project, "general"), PagesTestIds.ProjectSettingsPage),
        ["authorize"] = new(_ => "/authorize", AgentsTestIds.AuthorizePage),
    };

    // Opens a route and checks it against the touch rules: a route in TouchPendingRoutes must still break them (so a
    // finished route can't stay listed), any other route must follow them.
    public static async Task CheckAsync(IPage page, string screenId, Guid markdownAttachment)
    {
        var route = All[screenId];
        await page.GotoAsync(route.Url(markdownAttachment));
        await Assertions.Expect(page.GetByTestId(route.ReadyTestId)).ToBeVisibleAsync();

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
