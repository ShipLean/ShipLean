using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// Two creates at once for one project: the per-project lock lets exactly one through.
[TestFixture]
public class SprintConcurrentCreateApiTests : ApiFixture
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    [Test]
    public async Task Create_WhenTwoOverlappingAtOnce_ShouldAcceptOne()
    {
        var accepted = new List<int>();
        for (var i = 0; i < 15; i++)
        {
            var start = Today.AddDays(2000 + (i * 40));
            var a = Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Ship}/sprints", new SprintCreate($"Race A{i}", start, start.AddDays(10), ""));
            var b = Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Ship}/sprints", new SprintCreate($"Race B{i}", start.AddDays(5), start.AddDays(15), ""));
            var both = await Task.WhenAll(a, b);
            accepted.Add(both.Count(r => r.StatusCode == HttpStatusCode.OK));
            foreach (var r in both) { r.Dispose(); }
        }

        accepted.ShouldAllBe(n => n == 1);
    }

    [Test]
    public async Task Create_WhenCaseVariantNamesAtOnce_ShouldAcceptOne()
    {
        var outcomes = new List<string>();
        for (var i = 0; i < 15; i++)
        {
            var start = Today.AddDays(4000 + (i * 80));
            var a = Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Ship}/sprints", new SprintCreate($"Case{i}", start, start.AddDays(10), ""));
            var b = Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Ship}/sprints", new SprintCreate($"CASE{i}", start.AddDays(20), start.AddDays(30), ""));
            var both = await Task.WhenAll(a, b);
            outcomes.Add(string.Join("/", both.Select(r => (int)r.StatusCode).Order()));
            foreach (var r in both) { r.Dispose(); }
        }

        outcomes.ShouldAllBe(o => o == "200/409");
    }
}
