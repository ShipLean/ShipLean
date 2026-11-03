using System.Text.Json;
using ShipLean.Application.Features.Retros.Sprint;
using ShipLean.Contracts.Retros;
using ShipLean.UnitTest.Dtos.Boards;

namespace ShipLean.UnitTest.Retros;

[TestFixture]
public class RetroCategoriesTests
{
    [Test]
    public void Display_ShouldBeWentWellNotInPlanWentBadlyImprove()
    {
        RetroCategories.Display.ShouldBe([RetroCategory.WentWell, RetroCategory.NotInPlan, RetroCategory.WentBadly, RetroCategory.Improve]);
    }

    [Test]
    public void Columns_WhenBuilt_ShouldUseTheFixedOrder()
    {
        var columns = SprintRetroColumns.Build([], new Dictionary<Guid, DateTimeOffset>());

        columns.Select(column => column.Category).ShouldBe([RetroCategory.WentWell, RetroCategory.NotInPlan, RetroCategory.WentBadly, RetroCategory.Improve]);
        columns.ShouldAllBe(column => column.ToDecide == 0);
    }

    [Test]
    public void Columns_WhenPointsAreUndecided_ShouldCountThemPerColumn()
    {
        var retro = BoardDtoFixture.Read("retros", "sprint-retro.json").Deserialize<SprintRetroDto>(BoardDtoFixture.Options)!;

        var columns = SprintRetroColumns.Build(retro.Cards, new Dictionary<Guid, DateTimeOffset>());

        columns.Select(column => column.ToDecide).ShouldBe([.. retro.Categories.Select(column => column.ToDecide)]);
        columns.Sum(column => column.ToDecide).ShouldBe(retro.Kpis.PointsToDecide);
        retro.Kpis.PointsToDecide.ShouldBeGreaterThan(0);
    }
}
