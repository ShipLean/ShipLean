using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Journeys;

// A person's points on a sprint's Retro tab, next to an agent's: add a sprint-level and an item point, move the agent's
// point to the person's new theme, archive their own point. The agent's text never changes.
[TestFixture]
public class PeoplePointsJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private static readonly Guid ProjectId = SeedIds.Projects.Ship;
    private static readonly Guid MoveSprintId = SeedIds.Sprints.Sprint19;
    private static readonly Guid ArchiveSprintId = SeedIds.Sprints.Sprint20;

    // Each test has an ended sprint of its own, so the retro page holds only what the test put there.
    [Test]
    public async Task AddSprintPointAndMoveTheme_ShouldPersistAfterReload()
    {
        var agentText = "Review queue stalled every Thursday";
        var ownText = "Release job needed two manual retries";
        var agentPointId = await ArrangeAgentPointAsync(agentText, MoveSprintId);
        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync(Project, MoveSprintId);

        await retro.AddPoint.ClickAsync();
        await retro.AddPointText.FillAsync(ownText);
        await PickThemeAsync("Release pipeline", expectAdd: true);
        var created = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/points", "POST", () => retro.AddPointSave.ClickAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(retro.AddPointDialog).ToBeHiddenAsync();
        await Expect(retro.PointOf(ownText)).ToBeVisibleAsync();

        // The agent's point moves to the person's new theme.
        await retro.MoveTheme(agentText).ClickAsync();
        var moved = await Page.RunAndWaitForApiAsync($"/api/retros/points/{agentPointId}/theme", "PUT", PickExistingThemeAsync("Release pipeline"));
        moved.Ok.ShouldBeTrue(await moved.TextAsync());

        await Page.ReloadAsync();
        await retro.Panel.WaitForAsync();
        var ownGroup = GroupOf(retro, ownText);
        await Expect(ownGroup).ToBeVisibleAsync();
        await Expect(ownGroup.GetByTestId(RetrosTestIds.RetroPoint).Filter(new() { HasText = agentText })).ToHaveCountAsync(1);
        var theme = await ownGroup.GetAttributeAsync("data-theme");
        theme.ShouldNotBe("unthemed");
        await Expect(GroupOf(retro, agentText)).ToHaveAttributeAsync("data-theme", theme!);

        await using var connection = await OpenAsync();
        (await connection.QuerySingleAsync<string>("SELECT text FROM retro_point WHERE id = @agentPointId", new { agentPointId })).ShouldBe(agentText);
        (await connection.QuerySingleAsync<string>("SELECT t.name FROM retro_point p JOIN retro_theme t ON t.id = p.theme_id WHERE p.id = @agentPointId", new { agentPointId })).ShouldBe("Release pipeline");
    }

    [Test]
    public async Task AddItemPointAndArchiveOwn_ShouldKeepOnlyTheAgentsPoint()
    {
        var agentText = "Spike on the importer had no owner";
        var itemText = "Importer story was split too late";
        var agentPointId = await ArrangeAgentPointAsync(agentText, ArchiveSprintId);
        Guid storyId;
        await using (var connection = await OpenAsync())
        {
            storyId = await new WorkItemBuilder(ProjectId).WithType("story").WithTitle("Importer retro story").WithSprint(ArchiveSprintId).InsertAsync(connection);
            // The About list offers the items on the sprint's retro cards, so an agent has a retro on the story.
            var seed = new RetroSeed(connection, ProjectId);
            var storyRetro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: storyId, SprintId: ArchiveSprintId, AgentName: $"agent-{Guid.NewGuid():N}"));
            await seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentWell, "Importer fixtures were reusable"));
        }

        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync(Project, ArchiveSprintId);

        await retro.AddPoint.ClickAsync();
        await retro.AddPointCategory.SelectOptionAsync("wentWell");
        await retro.AddPointItem.SelectOptionAsync(storyId.ToString());
        await retro.AddPointText.FillAsync(itemText);
        var created = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/points", "POST", () => retro.AddPointSave.ClickAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(retro.PointOf(itemText)).ToBeVisibleAsync();

        // Only the person's own point can be archived; the agent's has no Archive button.
        await Expect(retro.Archive(agentText)).ToHaveCountAsync(0);
        var ownPointId = await retro.PointOf(itemText).GetAttributeAsync("data-point-id");
        var archived = await Page.RunAndWaitForApiAsync($"/api/retros/points/{ownPointId}/archive", "POST", () => retro.Archive(itemText).ClickAsync());
        archived.Ok.ShouldBeTrue($"Archive answered {archived.Status}");
        await Expect(retro.PointOf(itemText)).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await retro.Panel.WaitForAsync();
        await Expect(retro.PointOf(agentText)).ToBeVisibleAsync();
        await Expect(retro.PointOf(itemText)).ToHaveCountAsync(0);

        await using var check = await OpenAsync();
        (await check.ExecuteScalarAsync<bool>("SELECT archived_at IS NOT NULL FROM retro_point WHERE id = @id", new { id = Guid.Parse(ownPointId!) })).ShouldBeTrue();
        (await check.ExecuteScalarAsync<bool>("SELECT archived_at IS NULL FROM retro_point WHERE id = @agentPointId", new { agentPointId })).ShouldBeTrue();
    }

    private static ILocator GroupOf(SprintRetroPage retro, string pointText) =>
        retro.ThemeGroups.Filter(new() { Has = retro.PointOf(pointText) });

    // Types the name in the open theme picker; a new name is picked through its "Add" option.
    private async Task PickThemeAsync(string name, bool expectAdd)
    {
        var picker = Page.GetByTestId(RetroSharedTestIds.RetroThemePicker);
        await FocusPickerInputAsync(picker);
        await Page.Keyboard.TypeAsync(name);
        if (expectAdd)
        {
            await Expect(Page.GetByTestId(RetroSharedTestIds.RetroThemePickerAdd)).ToBeVisibleAsync();
        }

        await Page.Keyboard.PressAsync("Enter");
    }

    // The picker has no test id on its input, so the click lands where the input sits inside the picker's padding.
    private static Task FocusPickerInputAsync(ILocator picker) => picker.ClickAsync(new() { Position = new Position { X = 100, Y = 14 } });

    private Func<Task> PickExistingThemeAsync(string name) => async () =>
    {
        var picker = Page.GetByTestId(RetroSharedTestIds.RetroThemePicker);
        await FocusPickerInputAsync(picker);
        await Page.Keyboard.TypeAsync(name);
        await Page.Keyboard.PressAsync("Enter");
    };

    // An agent's retro on the sprint with one Went badly point; returns the point's id.
    private async Task<Guid> ArrangeAgentPointAsync(string text, Guid sprintId)
    {
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, ProjectId);
        var retroId = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, SprintId: sprintId, AgentName: $"agent-{Guid.NewGuid():N}"));
        return await seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.WentBadly, text, RootCause: "Nobody watched the queue"));
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
