using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// An action item sits at the story level: it shows in the Stories backlog tree, in the "No parent" group and in a Feature's points.
[TestFixture]
public class ActionItemBacklogTests : ApiFixture
{
    private static readonly string Tree = $"api/projects/{SeedIds.Projects.Ship}/backlog?mode=tree";
    private static readonly string Features = $"api/projects/{SeedIds.Projects.Ship}/backlogs/features?includeCompleted=true";

    [Test]
    public async Task Tree_WhenActionItemUnderFeature_ShouldShowAtStoryLevel()
    {
        var view = await Api.GetBacklogAsync(Tree);

        var rows = view.Rows.Select((row, index) => (row, index)).ToList();
        var feature = rows.Single(x => x.row.Key == "SHIP-104");
        var action = rows.Single(x => x.row.Key == "SHIP-89");
        action.row.Kind.ShouldBe(BacklogRowKind.Item);
        action.row.Type.ShouldNotBeNull().Id.ShouldBe(WorkItemTypeId.Action);
        action.row.Depth.ShouldBe(feature.row.Depth + 1);
        action.index.ShouldBeGreaterThan(feature.index);
    }

    [Test]
    public async Task Tree_WhenActionItemNoParent_ShouldShowInStoriesBacklog()
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Parentless action").WithPoints(2).InsertAsync(connection);

        var view = await Api.GetBacklogAsync(Tree);

        var item = view.Rows.Single(row => row.Id == id);
        item.Kind.ShouldBe(BacklogRowKind.Item);
        item.Type.ShouldNotBeNull().Id.ShouldBe(WorkItemTypeId.Action);
        var group = view.Rows.Single(row => row.Kind == BacklogRowKind.NoParentGroup);
        group.Rollups.ShouldContain(r => r.Type == WorkItemTypeId.Action && r.Open >= 1);
        var order = view.Rows.ToList();
        order.IndexOf(item).ShouldBeGreaterThan(order.IndexOf(group));
    }

    [Test]
    public async Task Levels_WhenActionItemEstimated_ShouldCountItsPointsInTheFeature()
    {
        var before = (await Api.GetBacklogAsync(Features)).Rows.Single(row => row.Key == "SHIP-104").ChildPoints;
        await using var connection = await OpenDatabaseAsync();
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Estimated action").WithParent(SeedIds.WorkItems.Ship104).WithPoints(5).InsertAsync(connection);

        var after = (await Api.GetBacklogAsync(Features)).Rows.Single(row => row.Key == "SHIP-104").ChildPoints;

        after.ShouldBe((before ?? 0m) + 5m);
    }
}
