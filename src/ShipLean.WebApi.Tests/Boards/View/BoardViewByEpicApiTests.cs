using Dapper;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Boards.View;

// The Features board laid out By Epic; the user's lane preference is set by the test, so the class has a database of its own.
[TestFixture]
public class BoardViewByEpicApiTests : ApiFixture
{
    private static readonly Guid Features = SeedIds.Boards.ShipFeatures;

    [Test]
    public async Task FeaturesBoard_WhenByEpic_ShouldListEmptyEpicLanes()
    {
        await using var connection = await OpenDatabaseAsync();
        var empty = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("epic").WithTitle("Empty epic").WithBoardPosition("col-new").WithBacklogOrder("99999999").InsertAsync(connection);
        await Api.SetLanePreferenceAsync(Features, "epic");

        var board = await Api.BoardAsync(Features);

        board.Lanes.Select(l => l.Kind).ShouldBe([BoardLaneKind.Epic, BoardLaneKind.Epic, BoardLaneKind.Epic, BoardLaneKind.Epic, BoardLaneKind.Epic, BoardLaneKind.NoEpic]);
        board.Lanes.Take(4).Select(l => l.Epic!.Key).ShouldBe(["SHIP-90", "SHIP-91", "SHIP-92", "SHIP-93"]);
        var lane = board.Lanes[4];
        lane.Epic!.Id.ShouldBe(empty);
        lane.Id.ShouldBe($"epic-{empty}");
        lane.CardCount.ShouldBe(0);
        lane.Cells.ShouldAllBe(c => c.Cards.Count == 0);
        lane.AcceptsDrops.ShouldBeTrue();
        board.Lanes[^1].Id.ShouldBe("lane-no-epic");
        board.Lane($"epic-{SeedIds.WorkItems.Ship90}").Cell("col-in-progress").Cards.Select(c => c.Key).ShouldBe(["SHIP-101"]);
    }
}
