using ShipLean.Application.Features.FeaturesOverview;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;

namespace ShipLean.UnitTest.Screens;

[TestFixture]
public class FeatureGroupsTests
{
    private static FeatureRowDto Row(int rank, StateCategory category) =>
        new(
            Guid.NewGuid(), $"SHIP-{rank}", $"Feature {rank}", 1, new StateRef(Guid.NewGuid(), category.ToString(), category), null, null, null, false, [], null, rank, null);

    [Test]
    public void Group_WhenFeaturesInEveryCategory_ShouldOrderActiveResolvedNewThenClosedFolded()
    {
        // Backlog (rank) order is not the group order: the board's column order would put New first.
        var rows = new[]
        {
            Row(1, StateCategory.Proposed), Row(2, StateCategory.Completed), Row(3, StateCategory.Resolved),
            Row(4, StateCategory.InProgress), Row(5, StateCategory.InProgress), Row(6, StateCategory.Completed),
        };

        var groups = FeatureGroups.Group(rows);

        groups.Select(g => (g.StateCategory, g.Label, g.Folded, g.Count)).ShouldBe(
            [("inProgress", "Active", false, 2), ("resolved", "Resolved", false, 1), ("proposed", "New", false, 1), ("completed", "Closed", true, 2)]);
        groups[0].Rows.Select(r => r.Rank).ShouldBe([4, 5]);
        groups[^1].Rows.ShouldBeEmpty();
    }

    [Test]
    public void Group_WhenAGroupIsEmpty_ShouldLeaveItOut()
    {
        var groups = FeatureGroups.Group([Row(2, StateCategory.Proposed), Row(1, StateCategory.InProgress)]);

        groups.Select(g => g.Label).ShouldBe(["Active", "New"]);
        FeatureGroups.Group([]).ShouldBeEmpty();
    }

    [Test]
    public void Group_WhenRowsGivenOutOfOrder_ShouldKeepRankOrderInsideAGroup()
    {
        var groups = FeatureGroups.Group([Row(7, StateCategory.InProgress), Row(3, StateCategory.InProgress)]);

        groups.Single().Rows.Select(r => r.Rank).ShouldBe([3, 7]);
    }
}
