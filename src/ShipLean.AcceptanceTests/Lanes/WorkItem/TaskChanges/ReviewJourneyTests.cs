using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.TaskChanges;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.TaskChanges;

// The reviewer's side of Changes: approving a task, a Bug taking commits too, and the narrow screen that shows no Changes.
[TestFixture]
public class ReviewJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddFakeCodeHost();

    [Test]
    public async Task ReviewerApproves_WhenNoOpenComments_ShouldCloseTask()
    {
        await SeedCommitsAsync(SeedIds.WorkItems.Ship172);
        var changes = new ChangesPage(Page);
        var side = new SideRailPanel(Page);
        await changes.OpenAsync(Project, "SHIP-172");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        (await StateOfAsync("SHIP-172")).ShouldBe("Active");

        var response = await changes.ApproveAsync(SeedIds.WorkItems.Ship172);

        response.Ok.ShouldBeTrue();
        (await StateOfAsync("SHIP-172")).ShouldBe("Closed");
        await new WorkItemScreen(Page).OpenAsync(Project, "SHIP-172");
        await Expect(side.Revisions.First).ToContainTextAsync($"Approved {FakeCodeHost.Shas.First[..7]}, {FakeCodeHost.Shas.Second[..7]}");
    }

    [Test]
    public async Task BugWithCommits_WhenOpened_ShouldShowChanges()
    {
        await SeedCommitsAsync(SeedIds.WorkItems.Ship153);
        var screen = new WorkItemScreen(Page);
        var section = new ChangesPanel(Page);
        await screen.OpenAsync(Project, "SHIP-153");

        await Expect(section.Section).ToBeVisibleAsync();
        await Expect(section.Rows).ToHaveCountAsync(2);
        await section.ReviewAsync();

        var changes = new ChangesPage(Page);
        await Expect(changes.Screen).ToBeVisibleAsync();
        await Expect(changes.DiffFiles.First).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChangesPage_WhenNarrowViewport_ShouldShowWiderScreenNotice()
    {
        await SeedCommitsAsync(SeedIds.WorkItems.Ship173);
        await Page.SetViewportSizeAsync(390, 800);
        var screen = new WorkItemScreen(Page);
        var section = new ChangesPanel(Page);
        await screen.OpenAsync(Project, "SHIP-173");
        await Expect(screen.Title).ToBeVisibleAsync();
        await Expect(section.Section).ToHaveCountAsync(0);

        var changes = new ChangesPage(Page);
        await changes.OpenNarrowAsync(Project, "SHIP-173");
        await Expect(changes.NarrowNotice).ToBeVisibleAsync();
        await Expect(changes.FileRows).ToHaveCountAsync(0);
        await changes.BackFromNarrowNoticeAsync();

        await Expect(screen.Title).ToBeVisibleAsync();
        await Expect(section.Section).ToHaveCountAsync(0);
    }

    private async Task SeedCommitsAsync(Guid itemId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var seed = new CodeChangesSeed(connection);
        await seed.CommitAsync(itemId, FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.First);
        await seed.CommitAsync(itemId, FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.Second);
    }

    private async Task<string> StateOfAsync(string key)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return await connection.QuerySingleAsync<string>("SELECT s.name FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.key = @key", new { key });
    }
}
