using Dapper;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Boards.View;

// Features arranged with closed and open children; the class has a database of its own so they never reach another test.
[TestFixture]
public class BoardViewOfferDoneApiTests : ApiFixture
{
    private static readonly Guid Features = SeedIds.Boards.ShipFeatures;

    [Test]
    public async Task FeaturesBoard_WhenEveryChildClosedAndNotInDone_ShouldOfferDone()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Wrapped up").WithBoardPosition("col-in-progress").InsertAsync(connection);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithParent(feature).WithStateNamed("Closed").InsertAsync(connection);
        var stillOpen = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Not yet").WithBoardPosition("col-in-progress").InsertAsync(connection);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithParent(stillOpen).WithStateNamed("Closed").InsertAsync(connection);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithParent(stillOpen).InsertAsync(connection);
        var inDone = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Already done").WithBoardPosition("col-done").InsertAsync(connection);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithParent(inDone).WithStateNamed("Closed").InsertAsync(connection);

        var board = await Api.BoardAsync(Features);

        board.Cards().Single(c => c.Id == feature).OfferDone.ShouldBeTrue();
        board.Cards().Single(c => c.Id == stillOpen).OfferDone.ShouldBeFalse();
        board.Cards().Single(c => c.Id == inDone).OfferDone.ShouldBeFalse();
        board.Cards().Single(c => c.Id == feature).Rollups.ShouldBe([new RollupCountDto(WorkItemTypeId.Story, 0, 1)]);
    }
}
