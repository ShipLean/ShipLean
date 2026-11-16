using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Asks;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Identity;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Decisions;

// The work item's asks date a question in the viewer's zone. The viewer is in Asia/Bangkok; the question was asked at
// ViewerZoneTestData.LateEveningUtc (20:00 UTC, so the next day in Bangkok).
[TestFixture]
public class AskViewerZoneTests : BangkokViewerTest
{
    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to the Relative calls in ItemAsks.
    [Test]
    public async Task ItemAsks_WhenViewerIsInBangkok_ShouldDateAQuestionByTheBangkokDay()
    {
        var (id, key) = await AskSeeding.SeedItemAsync(ConnectionString, assignee: SeedIds.Users.Joel);
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await new QuestionBuilder(id).Text("Which day is it?").AskedBy(SeedIds.Users.Joel, null, ViewerZoneTestData.LateEveningUtc).BuildAsync(connection);
        }

        await new WorkItemScreen(Page).OpenAsync("SHIP", key);

        var row = new DecisionsPanel(Page, id).QuestionRow(1);
        await Expect(row).ToContainTextAsync(ExpectedLabels.Relative(ViewerZoneTestData.LateEveningUtc, ViewerZoneTestData.Bangkok));
        await Expect(row).Not.ToContainTextAsync($"· {ExpectedLabels.Relative(ViewerZoneTestData.LateEveningUtc)}");
    }
}
