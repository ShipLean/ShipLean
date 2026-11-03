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

        // The column's own Add (the toolbar one is gone): Went badly, about the sprint.
        await retro.Board.AddIn("Went badly").ClickAsync();
        await retro.Board.AddText.FillAsync(ownText);
        var created = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/points", "POST", () => retro.Board.AddSave.ClickAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(retro.Board.AddText).ToBeHiddenAsync();
        await Expect(retro.PointOf(ownText)).ToBeVisibleAsync();

        // By category groups notes under their theme and shows no theme line; By work item shows the line, so the themes are set there.
        await retro.ViewByItem.ClickAsync();
        await retro.MoveTheme(ownText).ClickAsync();
        var set = await Page.RunAndWaitForApiAsync($"/api/retros/points/{await retro.PointOf(ownText).GetAttributeAsync("data-point-id")}/theme", "PUT", () => PickNewThemeAsync("Release pipeline"));
        set.Ok.ShouldBeTrue(await set.TextAsync());

        // The agent's point moves to the person's new theme.
        await retro.MoveTheme(agentText).ClickAsync();
        var moved = await Page.RunAndWaitForApiAsync($"/api/retros/points/{agentPointId}/theme", "PUT", PickExistingThemeAsync("Release pipeline"));
        moved.Ok.ShouldBeTrue(await moved.TextAsync());

        await Page.ReloadAsync();
        await retro.Panel.WaitForAsync();
        await retro.ViewByCategory.ClickAsync();
        // Both notes sit under one theme heading in their column: one chip, themed, named for the person's theme.
        var column = retro.Board.ColumnOf("Went badly");
        await Expect(column.GetByTestId(RetroSharedTestIds.RetroNote)).ToHaveCountAsync(2);
        await Expect(column.GetByTestId(RetroSharedTestIds.RetroThemeChip)).ToHaveCountAsync(1);
        await Expect(column.GetByTestId(RetroSharedTestIds.RetroThemeChip)).ToHaveAttributeAsync("data-themed", "true");
        await Expect(column.GetByTestId(RetroSharedTestIds.RetroThemeChip)).ToContainTextAsync("Release pipeline");

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

        await retro.Board.AddIn("Went well").ClickAsync();
        await retro.Board.AddAbout.SelectOptionAsync(storyId.ToString());
        await retro.Board.AddText.FillAsync(itemText);
        var created = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/points", "POST", () => retro.Board.AddSave.ClickAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(retro.PointOf(itemText)).ToBeVisibleAsync();

        // Only the person's own point can be archived; the agent's has no Archive button.
        await Expect(retro.Archive(agentText)).ToHaveCountAsync(0);
        var ownPointId = await retro.PointOf(itemText).GetAttributeAsync("data-point-id");
        await retro.PointOf(itemText).GetByTestId(RetroSharedTestIds.RetroNoteMore).ClickAsync();
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

    // Types a new name in the open theme picker and picks it through its "Add" option.
    private async Task PickNewThemeAsync(string name)
    {
        var picker = Page.GetByTestId(RetroSharedTestIds.RetroThemePicker);
        await FocusPickerInputAsync(picker);
        await Page.Keyboard.TypeAsync(name);
        await Expect(Page.GetByTestId(RetroSharedTestIds.RetroThemePickerAdd)).ToBeVisibleAsync();
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
