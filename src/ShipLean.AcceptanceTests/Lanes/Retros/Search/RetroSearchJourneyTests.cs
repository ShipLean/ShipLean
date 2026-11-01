using ShipLean.AcceptanceTests.Harness;
using Npgsql;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Search;

// A retro whose point never says the searched word: only its raw failure log does. Search still finds the point, and the
// retro that opens beside the results shows the failure log open. The retro is seeded through SQL (RetroSeed).
[TestFixture]
public class RetroSearchJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string LogOnlyWord = "quokkafrost";
    private const string PointText = "The deploy step stalled for an hour.";

    [Test]
    public async Task Search_WhenWordOnlyInFailureLog_ShouldOpenRetroWithLog()
    {
        await ArrangeAsync();
        var search = new RetrosSearchPage(Page);

        await search.OpenAsync(Project, ("q", LogOnlyWord));

        await Expect(search.Row).ToHaveCountAsync(1);
        await Expect(search.RowOf(PointText)).ToBeVisibleAsync();
        await search.RowOf(PointText).ClickAsync();
        await Expect(search.Selected).ToBeVisibleAsync();
        await Expect(search.SelectedCard).ToBeVisibleAsync();
        var log = search.SelectedCard.GetByTestId(ShipLean.TestSupport.TestIds.RetroSharedTestIds.RetroCardLog);
        await Expect(log).ToBeVisibleAsync();
        await Expect(log).ToContainTextAsync(LogOnlyWord);
    }

    private async Task ArrangeAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Deploy pipeline hardening").WithSprint(SeedIds.Sprints.Sprint23).InsertAsync(connection);
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: story, SprintId: SeedIds.Sprints.Sprint23, AgentName: "agent-log-only"));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, PointText));
        await seed.FailureAsync(retro, RetroFailureKind.Blocker, $"The {LogOnlyWord} handshake timed out twice.");
    }
}
