using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Taskboard;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// D31: a run the store lists for the item but no longer returns (the store is up) answers 200 with expired=true. A run that is not listed stays
// not found, and a listed run with the store down stays 409. The source lists a run and returns nothing for it, which is what both stores do
// (ClickHouse keeps the run index past retention; Tempo/Loki keeps the cached link row).
[TestFixture]
public class ExpiredRunTests : InspectionViewsFixture
{
    private volatile bool storeUp = true;

    protected override Action<IServiceCollection>? ConfigureTestServices
    {
        get
        {
            var baseConfigure = base.ConfigureTestServices;
            return services =>
            {
                baseConfigure?.Invoke(services);
                services.RemoveAll<ITraceSource>();
                services.AddSingleton<ITraceSource>(new ExpiredSource(Fake, () => storeUp));
            };
        }
    }

    [Test]
    public async Task Run_WhenListedButTheStoreNoLongerReturnsIt_ShouldAnswerExpired()
    {
        var page = await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}");

        page.Expired.ShouldBeTrue();
        page.Turns.ShouldBeEmpty();
        page.Header.Agent.ShouldBe("claude-code");
    }

    [Test]
    public async Task Run_WhenNotListed_ShouldStayNotFound()
    {
        using var response = await Api.GetAsync($"{RunsUrl}/no-such-run");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Run_WhenListedAndTheStoreIsDown_ShouldStayConflict()
    {
        storeUp = false;
        using var response = await Api.GetAsync($"{RunsUrl}/{RunId}");
        storeUp = true;

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Row_WhenTheRunExpired_ShouldBeNotFound()
    {
        using var response = await Api.GetAsync($"{RunsUrl}/{RunId}/rows/x");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed class ExpiredSource(ITraceSource listing, Func<bool> up) : ITraceSource
    {
        public Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct) => listing.FindRunsAsync(query, ct);

        public Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct) => Task.FromResult<RawRun?>(null);

        public Task<StoreHealth> CheckAsync(CancellationToken ct) => Task.FromResult(new StoreHealth(up(), null, null, up() ? null : "connection refused"));

        public Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FieldCheck>>([]);
    }
}
