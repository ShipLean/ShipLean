using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// A Removed Feature (SHIP-101) and an orphan next to a closed story: only this class changes the seed that way.
[TestFixture]
public class StoriesBacklogRemovedFeatureApiTests : ApiFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog?mode=tree";

    [OneTimeSetUp]
    public async Task RemoveFeature()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE work_item SET state_id = (SELECT s.id FROM state s WHERE s.project_id = work_item.project_id AND s.type = 'feature'
                                             AND s.category = 'removed' AND s.archived_at IS NULL LIMIT 1)
            WHERE key = 'SHIP-101'
            """;
        (await command.ExecuteNonQueryAsync()).ShouldBe(1);
    }

    [Test]
    public async Task Backlog_WhenFeatureRemoved_ShouldStitchStoriesUnderEpic()
    {
        var view = await Api.GetBacklogAsync(Url);

        var epic = view.Rows.Select((row, index) => (row, index)).Single(x => x.row.Key == "SHIP-90");
        view.Rows.ShouldNotContain(row => row.Key == "SHIP-101");
        var below = view.Rows.Skip(epic.index + 1).TakeWhile(row => row.Depth > 0).ToList();
        below.Select(row => row.Key).ShouldBe(["SHIP-141", "SHIP-144", "SHIP-158", "SHIP-160"], ignoreOrder: true);
        below.ShouldAllBe(row => row.Depth == 1);
        below.ShouldAllBe(row => row.Parent != null && row.Parent.Key == "SHIP-101");
        view.Rows.ShouldNotContain(row => row.Kind == BacklogRowKind.NoParentGroup);
    }
}
