using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Backlog.Stories;

namespace ShipLean.WebApi.Tests.Backlog.Levels;

// The Features backlog in tree mode. The seed nests SHIP-108 under SHIP-103 under Epic SHIP-92; this class also completes SHIP-103,
// takes SHIP-104 out from under its Epic and tags SHIP-108 Deep.
[TestFixture]
public class LevelBacklogTreeApiTests : ApiFixture
{
    private static readonly string Root = $"api/projects/{SeedIds.Projects.Ship}/backlogs";

    private const string DeepFilter = """{"match":"all","clauses":[{"field":"tags","op":"contains","value":"Deep"}]}""";

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE work_item SET state_id = (SELECT s.id FROM state s WHERE s.project_id = work_item.project_id AND s.type = 'feature'
                                             AND s.category = 'completed' AND s.archived_at IS NULL LIMIT 1)
            WHERE key = 'SHIP-103';
            UPDATE work_item SET parent_id = NULL, parent_type = NULL WHERE key = 'SHIP-104';
            UPDATE work_item SET tags = ARRAY['Deep'] WHERE key = 'SHIP-108';
            """;
        (await command.ExecuteNonQueryAsync()).ShouldBe(3);
    }

    [Test]
    public async Task FeaturesTree_WhenRead_ShouldNestFeaturesUnderTheirEpicsWithNoParentLast()
    {
        var tree = await Api.GetBacklogAsync($"{Root}/features?mode=tree");

        tree.Mode.ShouldBe(BacklogMode.Tree);
        tree.Rows.Select(row => (row.Key, row.Kind, row.Depth)).ShouldBe(
        [
            ("SHIP-90", BacklogRowKind.Group, 0),
            ("SHIP-101", BacklogRowKind.Item, 1),
            ("SHIP-91", BacklogRowKind.Group, 0),
            ("SHIP-102", BacklogRowKind.Item, 1),
            ("SHIP-92", BacklogRowKind.Group, 0),
            ("SHIP-103", BacklogRowKind.Group, 1),
            ("SHIP-108", BacklogRowKind.Item, 2),
            ("", BacklogRowKind.NoParentGroup, 0),
            ("SHIP-104", BacklogRowKind.Item, 1),
        ]);
        tree.TotalCount.ShouldBe(4);
    }

    [Test]
    public async Task FeaturesTree_WhenCompletedShown_ShouldShowTheCompletedParentAsAFeatureRow()
    {
        var tree = await Api.GetBacklogAsync($"{Root}/features?mode=tree&includeCompleted=true");

        tree.Rows.Single(row => row.Key == "SHIP-103").Kind.ShouldBe(BacklogRowKind.Item);
        tree.TotalCount.ShouldBe(5);
    }

    [Test]
    public async Task FeaturesTree_WhenFiltered_ShouldKeepTheMatchesAncestorsAndListRank()
    {
        var list = await Api.GetBacklogAsync($"{Root}/features");
        var tree = await Api.GetBacklogAsync($"{Root}/features?mode=tree&filter={Uri.EscapeDataString(DeepFilter)}");

        tree.Rows.Select(row => (row.Key, row.Kind, row.Depth)).ShouldBe(
        [
            ("SHIP-92", BacklogRowKind.Group, 0),
            ("SHIP-103", BacklogRowKind.Group, 1),
            ("SHIP-108", BacklogRowKind.Item, 2),
        ]);
        tree.Rows[2].Rank.ShouldBe(list.Rows.Single(row => row.Key == "SHIP-108").Rank);
        tree.Rows[0].Rank.ShouldBeNull();
    }

    [Test]
    public async Task FeaturesTree_WhenRead_ShouldGiveRowsTheListsRollupsAndCountFeaturesOnNoParent()
    {
        var list = await Api.GetBacklogAsync($"{Root}/features");
        var epics = await Api.GetBacklogAsync($"{Root}/epics");
        var tree = await Api.GetBacklogAsync($"{Root}/features?mode=tree");

        var feature = tree.Rows.Single(row => row.Key == "SHIP-101");
        feature.Rollups.ShouldBe(list.Rows.Single(row => row.Key == "SHIP-101").Rollups);
        feature.ChildPoints.ShouldBe(list.Rows.Single(row => row.Key == "SHIP-101").ChildPoints);
        tree.Rows.Single(row => row.Key == "SHIP-90").Rollups.ShouldBe(epics.Rows.Single(row => row.Key == "SHIP-90").Rollups);
        tree.Rows.Single(row => row.Kind == BacklogRowKind.NoParentGroup).Rollups.ShouldBe([new RollupCountDto(WorkItemTypeId.Feature, 1, 0, 0, 0)]);
    }

    [TestCase("epics?mode=tree")]
    [TestCase("features?mode=planning")]
    public async Task LevelBacklog_WhenModeNotOffered_ShouldAnswerValidation(string path)
    {
        using var response = await Api.GetAsync($"{Root}/{path}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
