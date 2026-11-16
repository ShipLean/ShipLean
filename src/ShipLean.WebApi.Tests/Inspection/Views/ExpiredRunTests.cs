using System.Net;
using Dapper;
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
    private volatile int endedDaysAgo = 45;

    protected override Action<IServiceCollection>? ConfigureTestServices
    {
        get
        {
            var baseConfigure = base.ConfigureTestServices;
            return services =>
            {
                baseConfigure?.Invoke(services);
                services.RemoveAll<ITraceSource>();
                services.AddSingleton<ITraceSource>(new ExpiredSource(Fake, () => storeUp, () => endedDaysAgo));
            };
        }
    }

    [SetUp]
    public void ResetTheSource()
    {
        storeUp = true;
        endedDaysAgo = 45;
    }

    [Test]
    public async Task Run_WhenListedButTheStoreNoLongerReturnsIt_ShouldAnswerExpired()
    {
        await BackdateItemAsync();
        var page = await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}");

        page.Expired.ShouldBeTrue();
        page.Turns.ShouldBeEmpty();
        page.Header.Agent.ShouldBe("claude-code");
    }

    [Test]
    public async Task Run_WhenItEndedLongBeforeTheSearchWindow_ShouldStillAnswerExpired()
    {
        // Ended 45 days ago, item 90 days old: the 30-day listing misses it, the wider expired check (retention + 30 days) finds it.
        await BackdateItemAsync();
        var runs = await Api.GetAsync<AgentRunsSectionDto>(RunsUrl);

        runs.Rows.ShouldBeEmpty();
        (await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}")).Expired.ShouldBeTrue();
    }

    [Test]
    public async Task Run_WhenListedAndRecentButTheStoreReturnsNothing_ShouldBeConflictNotExpired()
    {
        endedDaysAgo = 0;
        using var response = await Api.GetAsync($"{RunsUrl}/{RunId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("return this run");
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
        await BackdateItemAsync();
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

    private async Task BackdateItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE work_item SET created_at = @at WHERE key = @key", new { at = TimeProvider.System.GetUtcNow().AddDays(-90), key = Item });
    }

    // Lists the fixture's run as ended some days ago, and only when the query window reaches back to that end, as a real store does.
    private sealed class ExpiredSource(ITraceSource listing, Func<bool> up, Func<int> endedDaysAgo) : ITraceSource
    {
        public async Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct)
        {
            var found = await listing.FindRunsAsync(query with { From = query.From.AddDays(-365) }, ct);
            var ended = TimeProvider.System.GetUtcNow().AddDays(-endedDaysAgo());
            var runs = found.Runs.Select(r => r with { StartedAt = ended.AddMinutes(-5), EndedAt = ended }).Where(r => r.EndedAt >= query.From).ToList();
            return found with { Runs = runs };
        }

        public Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct) => Task.FromResult<RawRun?>(null);

        public Task<StoreHealth> CheckAsync(CancellationToken ct) => Task.FromResult(new StoreHealth(up(), null, null, up() ? null : "connection refused"));

        public Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FieldCheck>>([]);
    }
}
