using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Taskboard;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// A store that is down answers null for a run; the views say the store is unreachable, never that the run is gone.
[TestFixture]
public class StoreDownTests : InspectionViewsFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices
    {
        get
        {
            var baseConfigure = base.ConfigureTestServices;
            return services =>
            {
                baseConfigure?.Invoke(services);
                services.RemoveAll<ITraceSource>();
                services.AddSingleton<ITraceSource>(new DownSource());
            };
        }
    }

    [Test]
    public async Task Run_WhenTheStoreIsDown_ShouldSayItIsUnreachableNotNotFound()
    {
        using var response = await Api.GetAsync($"{RunsUrl}/{RunId}");
        using var row = await Api.GetAsync($"{RunsUrl}/{RunId}/rows/x");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("connection refused");
        row.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Section_WhenTheStoreIsDown_ShouldReportItUnreachableWithTheProblem()
    {
        var section = await Api.GetAsync<AgentRunsSectionDto>(RunsUrl);

        section.StoreReachable.ShouldBeFalse();
        section.Problem.ShouldBe("connection refused");
        section.Rows.ShouldBeEmpty();
    }

    private sealed class DownSource : ITraceSource
    {
        public Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct) => Task.FromResult(new RunSearch([], false, "connection refused"));

        public Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct) => Task.FromResult<RawRun?>(null);

        public Task<StoreHealth> CheckAsync(CancellationToken ct) => Task.FromResult(new StoreHealth(false, null, null, "connection refused"));

        public Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FieldCheck>>([]);
    }
}
