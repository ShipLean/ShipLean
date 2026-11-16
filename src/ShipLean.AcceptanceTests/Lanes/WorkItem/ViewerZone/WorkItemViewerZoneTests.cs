using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Identity;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.ViewerZone;

// The work item page dates a comment in the viewer's zone. The viewer is in Asia/Bangkok; the comment is written at
// ViewerZoneTestData.LateEveningUtc (20:00 UTC, so the next day in Bangkok).
[TestFixture]
public class WorkItemViewerZoneTests : BangkokViewerTest
{
    // Mutation: pass TimeZoneInfo.Utc instead of the viewer's zone in CommentRow.ToDto.
    [Test]
    public async Task WorkItem_WhenViewerIsInBangkok_ShouldDateACommentByTheBangkokDay()
    {
        var item = await AskSeeding.SeedItemAsync(ConnectionString, title: "Zone comment item", assignee: SeedIds.Users.Joel);
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await new CommentBuilder(item.Id).WithAuthor(SeedIds.Users.Joel).WithBody("Written late in the evening, UTC")
                .WithCreatedAt(ViewerZoneTestData.LateEveningUtc).InsertAsync(connection);
        }

        await new WorkItemScreen(Page).OpenAsync("SHIP", item.Key);

        var comment = new DiscussionPanel(Page).Comments.Filter(new LocatorFilterOptions { HasText = "Written late in the evening, UTC" });
        await Expect(comment.GetByTestId(WorkItemTestIds.CommentWhen)).ToHaveTextAsync(ExpectedLabels.Relative(ViewerZoneTestData.LateEveningUtc, ViewerZoneTestData.Bangkok));
    }
}
