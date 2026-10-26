using System.Net;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Sprints.Metrics;

// The metric reads over the seeded SHIP history (db/seed/0008_metric_history.sql). Only this class changes SHIP's velocity override.
[TestFixture]
public class SprintMetricsApiTests : ApiFixture
{
    [Test]
    public async Task Metrics_WhenSeededShip_ShouldMatchSeedHistory()
    {
        (Guid Sprint, decimal Committed, decimal Completed)[] expected =
        [
            (SeedIds.Sprints.Sprint19, 18, 16),
            (SeedIds.Sprints.Sprint20, 22, 19),
            (SeedIds.Sprints.Sprint21, 20, 20),
            (SeedIds.Sprints.Sprint22, 24, 22),
            (SeedIds.Sprints.Sprint23, 23, 21),
        ];

        foreach (var (sprint, committed, completed) in expected)
        {
            var metrics = await Api.GetAsync<SprintMetricsDto>($"api/sprints/{sprint}/metrics");
            (metrics.SprintId, metrics.Committed, metrics.Completed).ShouldBe((sprint, committed, completed));
        }

        var current = await Api.GetAsync<SprintMetricsDto>($"api/sprints/{SeedIds.Sprints.Sprint24}/metrics");
        current.Committed.ShouldBe(21m);
        current.Burndown.Take(3).Select(p => p.Remaining).ShouldBe([21m, 18m, 16m]);
        var velocity = await Api.GetAsync<VelocityDto>($"api/projects/{SeedIds.Projects.Ship}/velocity");
        velocity.Sprints.Select(s => (s.SprintId, s.Committed, s.Completed)).ShouldBe(
            [(SeedIds.Sprints.Sprint21, 20m, 20m), (SeedIds.Sprints.Sprint22, 24m, 22m), (SeedIds.Sprints.Sprint23, 23m, 21m)]);
        velocity.Average.ShouldBe(21m);
    }

    [Test]
    public async Task Velocity_WhenOverrideSet_ShouldUseIt()
    {
        using var set = await Api.PutJsonAsync($"api/projects/{SeedIds.Projects.Ship}/forecast", new VelocityOverridePut(30m));
        set.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var velocity = await Api.GetAsync<VelocityDto>($"api/projects/{SeedIds.Projects.Ship}/velocity");
        var forecast = await Api.GetAsync<ForecastDto>($"api/projects/{SeedIds.Projects.Ship}/forecast");

        velocity.Override.ShouldBe(30m);
        var hypothetical = forecast.Sprints.Where(s => s.SprintId is null).ToList();
        hypothetical.ShouldNotBeEmpty();
        hypothetical.ShouldAllBe(s => s.Room == 30m && !s.FromCapacity);
    }
}
