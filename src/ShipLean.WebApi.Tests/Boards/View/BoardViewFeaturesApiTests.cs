using Dapper;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Boards.View;

// The SHIP Features and Epics boards, with the items each test arranges for itself.
[TestFixture]
public class BoardViewFeaturesApiTests : ApiFixture
{
    private static readonly Guid Features = SeedIds.Boards.ShipFeatures;

    [Test]
    public async Task FeaturesBoard_WhenNoLanePreference_ShouldHoldEveryCardInOneLane()
    {
        var board = await Api.BoardAsync(Features);

        board.Lanes.Select(l => l.Kind).ShouldBe([BoardLaneKind.Default]);
        board.Cards().Select(c => c.Key).ShouldBe(["SHIP-104", "SHIP-108", "SHIP-101", "SHIP-102", "SHIP-103"], ignoreOrder: true);
    }

    [Test]
    public async Task FeaturesBoard_WhenNestedFeature_ShouldBeItsOwnCardAndCountInParentRollups()
    {
        var board = await Api.BoardAsync(Features);

        var nested = board.Cards().Single(c => c.Key == "SHIP-108");
        nested.Parent!.Key.ShouldBe("SHIP-103");
        nested.Parent.Type.Id.ShouldBe(WorkItemTypeId.Feature);
        var reporting = board.Cards().Single(c => c.Key == "SHIP-103");
        reporting.Rollups.ShouldContain(r => r.Type == WorkItemTypeId.Feature && r.Open == 1 && r.Closed == 0);
        reporting.Rollups.ShouldNotContain(r => r.Type == WorkItemTypeId.Task);
        reporting.Assignee.ShouldBeNull();
    }

    [Test]
    public async Task FeaturesBoard_WhenStoriesBelow_ShouldRollUpCountsAndPoints()
    {
        var board = await Api.BoardAsync(Features);

        var checkout = board.Cards().Single(c => c.Key == "SHIP-101");
        checkout.Rollups.Select(r => r.Type).ShouldBe([WorkItemTypeId.Story, WorkItemTypeId.Bug]);
        await using var connection = await OpenDatabaseAsync();
        var points = await connection.ExecuteScalarAsync<decimal>(
            "SELECT sum(story_points) FROM work_item WHERE parent_id = @id AND type IN ('story', 'bug')", new { id = SeedIds.WorkItems.Ship101 });
        checkout.ChildPoints.ShouldBe(points);
        checkout.SumWarn.ShouldBe(points > checkout.Points!.Value * 1.25m);
        checkout.OfferDone.ShouldBeFalse();
    }

    [Test]
    public async Task FeaturesBoard_WhenFilteredByEpicOrOwner_ShouldHideTheRestButKeepCounts()
    {
        var all = await Api.BoardAsync(Features);

        var byEpic = await Api.BoardAsync(Features, $"?epic={SeedIds.WorkItems.Ship90}");

        byEpic.Cards().Select(c => c.Key).ShouldBe(["SHIP-101"]);
        byEpic.Columns.Select(c => c.Count).ShouldBe(all.Columns.Select(c => c.Count));
        (await Api.BoardAsync(Features, "?epic=none")).Cards().ShouldBeEmpty();
        (await Api.BoardAsync(Features, "?owner=none")).Cards().Count().ShouldBe(all.Cards().Count());
        (await Api.BoardAsync(Features, $"?owner=none&owner={Guid.NewGuid()}")).Cards().Count().ShouldBe(all.Cards().Count());
    }

    [Test]
    public async Task EpicsBoard_WhenLoaded_ShouldRollUpFeaturesStoriesAndBugsInOneLane()
    {
        var board = await Api.BoardAsync(SeedIds.Boards.ShipEpics);

        board.Lanes.Select(l => l.Kind).ShouldBe([BoardLaneKind.Default]);
        var insights = board.Cards().Single(c => c.Key == "SHIP-92");
        insights.Rollups.Select(r => r.Type).ShouldBe([WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug]);
        insights.Rollups[0].ShouldBe(new RollupCountDto(WorkItemTypeId.Feature, 2, 0));
        insights.Parent.ShouldBeNull();
    }
}
