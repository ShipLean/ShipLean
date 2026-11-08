using Dapper;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Backlog.Stories;

namespace ShipLean.WebApi.Tests.Backlog.Levels;

// The seed nests Feature SHIP-108 under SHIP-103 under Epic SHIP-92; this class moves SHIP-103 into a Triage state.
[TestFixture]
public class LevelBacklogTriageTreeApiTests : ApiFixture
{
    private static readonly string Root = $"api/projects/{SeedIds.Projects.Ship}/backlogs";

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteAsync(
            """
            WITH triage AS (
                INSERT INTO state (id, project_id, type, name, category, sort_order, is_intake)
                SELECT gen_random_uuid(), id, 'feature', 'Triage', 'proposed', -1, true FROM project WHERE key = 'SHIP'
                RETURNING id
            )
            UPDATE work_item SET state_id = (SELECT id FROM triage) WHERE key = 'SHIP-103'
            """)).ShouldBe(1);
    }

    [Test]
    public async Task FeaturesTree_WhenParentIsInTriage_ShouldGroupTheChildUnderTheTriageParent()
    {
        var tree = await Api.GetBacklogAsync($"{Root}/features?mode=tree");

        tree.Rows.Select(row => (row.Key, row.Kind, row.Depth)).ShouldContain(("SHIP-103", BacklogRowKind.Group, 1));
        tree.Rows.Single(row => row.Key == "SHIP-103").State!.Name.ShouldBe("Triage");
        var child = tree.Rows.ToList().FindIndex(row => row.Key == "SHIP-108");
        tree.Rows[child - 1].Key.ShouldBe("SHIP-103");
        tree.Rows.Where(row => row.Kind == BacklogRowKind.NoParentGroup).ShouldBeEmpty(
            "SHIP-108 must not fall under No parent");
    }

    [Test]
    public async Task FeaturesList_WhenParentIsInTriage_ShouldHideTheParentRow()
    {
        var list = await Api.GetBacklogAsync($"{Root}/features");

        list.Rows.Select(row => row.Key).ShouldNotContain("SHIP-103");
        list.Rows.Select(row => row.Key).ShouldContain("SHIP-108");
    }
}
