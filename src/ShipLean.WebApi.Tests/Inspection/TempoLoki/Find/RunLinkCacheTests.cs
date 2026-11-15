using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

[TestFixture(TestName = "Tempo and Loki run-link cache")]
public sealed class RunLinkCacheTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static RunSummary Run() => new("s1", "s1", "claude-code", null, [], Now.AddHours(-1), Now.AddMinutes(-50), 1, 0, 0, null, null, [new RunItemLink("SHIP-1", LinkSource.Tagged)]);

    private static (TempoLokiRunFinder Finder, StubStores Stores) Build(FakeTimeProvider time, InMemoryRunLinkStore links)
    {
        var stores = new StubStores();
        var options = new InspectionOptions { TempoUrl = "http://tempo.test", LokiUrl = "http://loki.test" };
        var clients = StoreClients.Create(options, stores);
        var finder = new TempoLokiRunFinder(new TempoRunFinder(clients, options), new LokiRunFinder(clients, options), new RunLinkCache(links, time), options);
        return (finder, stores);
    }

    private static RunQuery Query() => new("SHIP", "SHIP-1", Now.AddDays(-1), Now, null, null);

    [Test]
    public async Task Find_WhenCalledAgainWithinTenMinutes_MakesNoStoreRequest()
    {
        var time = new FakeTimeProvider(Now);
        var links = new InMemoryRunLinkStore();
        var cache = new RunLinkCache(links, time);
        await cache.SaveAsync("SHIP", "SHIP-1", [Run()], new Dictionary<string, IReadOnlyList<string>>(), CancellationToken.None);
        var (finder, stores) = Build(time, links);

        time.Advance(TimeSpan.FromMinutes(9));
        var search = await finder.FindAsync(Query(), CancellationToken.None);

        search.Runs.Select(r => r.RunId).ShouldBe(["s1"]);
        stores.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task Find_WhenTheCachedRunsAreTenMinutesOld_AsksTheStoresAgain()
    {
        var time = new FakeTimeProvider(Now);
        var links = new InMemoryRunLinkStore();
        var cache = new RunLinkCache(links, time);
        await cache.SaveAsync("SHIP", "SHIP-1", [Run()], new Dictionary<string, IReadOnlyList<string>>(), CancellationToken.None);
        var (finder, stores) = Build(time, links);

        time.Advance(TimeSpan.FromMinutes(10));
        var search = await finder.FindAsync(Query(), CancellationToken.None);

        search.Runs.ShouldBeEmpty();
        stores.Requests.ShouldNotBeEmpty();
    }

    [Test]
    public async Task Find_WhenTheCachedRunIsOutsideTheWindow_ReturnsNothingWithoutAskingTheStores()
    {
        var time = new FakeTimeProvider(Now);
        var links = new InMemoryRunLinkStore();
        await new RunLinkCache(links, time).SaveAsync("SHIP", "SHIP-1", [Run()], new Dictionary<string, IReadOnlyList<string>>(), CancellationToken.None);
        var (finder, stores) = Build(time, links);

        var search = await finder.FindAsync(Query() with { From = Now.AddHours(-10), To = Now.AddHours(-5) }, CancellationToken.None);

        search.Runs.ShouldBeEmpty();
        stores.Requests.ShouldBeEmpty();
    }
}
