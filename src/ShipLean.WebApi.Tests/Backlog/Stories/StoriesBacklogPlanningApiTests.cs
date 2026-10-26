using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// Planning mode over the seeded sprints: 24 is current, 25 and 26 are future, 19 to 23 are past.
[TestFixture]
public class StoriesBacklogPlanningApiTests : ApiFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog?mode=planning";

    [Test]
    public async Task Backlog_WhenPlanning_ShouldOrderSectionsCurrentFutureThenBacklog()
    {
        var view = await Api.GetBacklogAsync(Url);

        view.Rows.ShouldBeEmpty();
        view.Sections.Select(s => s.Title).ShouldBe(["Sprint 24", "Sprint 25", "Sprint 26", "Backlog"]);
        view.Sections.Select(s => s.Sprint?.Status).ShouldBe([SprintStatus.Current, SprintStatus.Future, SprintStatus.Future, null]);
        view.Sections[0].Id.ShouldBe(SeedIds.Sprints.Sprint24.ToString());
        view.Sections[^1].Id.ShouldBe("backlog");
        view.Sections.ShouldAllBe(s => s.Rows.All(row => row.Depth == 0));
        view.Sections.ShouldAllBe(s => s.Rows.Select(row => row.Rank).SequenceEqual(Enumerable.Range(1, s.Rows.Count).Select(n => (int?)n)));
        view.TotalCount.ShouldBe(view.Sections.Sum(s => s.Rows.Count));
        view.Forecast.ShouldNotBeNull();
        view.Velocity.ShouldNotBeNull();
    }

    [Test]
    public async Task Backlog_WhenPlanning_ShouldSumPointsAndLeaveCapacityNullWhenNoneEntered()
    {
        var view = await Api.GetBacklogAsync(Url);

        var current = view.Sections[0];
        current.Capacity.ShouldBe(23m);
        current.Points.ShouldBeGreaterThanOrEqualTo(current.Rows.Sum(row => row.Points ?? 0));
        view.Sections[1].Capacity.ShouldBe(22m);
        view.Sections[2].Capacity.ShouldBeNull();
        view.Sections[2].Rows.ShouldBeEmpty();
        view.Sections[^1].Capacity.ShouldBeNull();
        view.Sections[^1].Rows.ShouldAllBe(row => row.Sprint == null);
    }

    [Test]
    public async Task Backlog_WhenPlanning_ShouldSumCommittedPointsIgnoringFilterAndCompleted()
    {
        const string noneMatch = """{"match":"all","clauses":[{"field":"tags","op":"contains","value":"NoSuchTag"}]}""";
        var plain = await Api.GetBacklogAsync(Url);
        var withCompleted = await Api.GetBacklogAsync($"{Url}&includeCompleted=true");
        var filtered = await Api.GetBacklogAsync($"{Url}&filter={Uri.EscapeDataString(noneMatch)}");

        var current = withCompleted.Sections[0];
        plain.Sections[0].Points.ShouldBe(current.Points);
        filtered.Sections[0].Points.ShouldBe(current.Points);
        filtered.Sections[0].Rows.ShouldBeEmpty();
        current.Points.ShouldBe(current.Rows.Sum(row => row.Points ?? 0));
        plain.Sections[0].Points.ShouldBeGreaterThan(plain.Sections[0].Rows.Sum(row => row.Points ?? 0));
        filtered.Sections[^1].Points.ShouldBe(0m);
    }
}
