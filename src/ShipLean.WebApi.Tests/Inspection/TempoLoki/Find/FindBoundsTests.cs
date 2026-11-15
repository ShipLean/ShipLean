using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

// What keeps one find from running away (SHIP-1559 review): a cap on trace fetches, a deadline, a request timeout, the project scope
// of fetch-by-id, and escaping of the item key.
[TestFixture(TestName = "Tempo and Loki find bounds")]
public sealed class FindBoundsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly InspectionOptions Options = new() { TempoUrl = "http://tempo.test", LokiUrl = "http://loki.test" };

    private static TempoLokiRunFinder Finder(ScriptedStores stores, FakeTimeProvider time)
    {
        var clients = StoreClients.Create(Options, stores);
        return new TempoLokiRunFinder(new TempoRunFinder(clients, Options), new LokiRunFinder(clients, Options), new RunLinkCache(new InMemoryRunLinkStore(), time), Options, time);
    }

    private static RunQuery Query(string item = "SHIP-1") => new("SHIP", item, Now.AddDays(-1), Now, null, null);

    private static HttpResponseMessage Answer(HttpRequestMessage request, Func<string, string> trace, string search, string loki)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/api/search", StringComparison.Ordinal))
        {
            return ScriptedStores.Json(search);
        }
        if (path.Contains("/api/traces/", StringComparison.Ordinal))
        {
            return ScriptedStores.Json(trace(path[(path.LastIndexOf('/') + 1)..]));
        }
        return ScriptedStores.Json(loki);
    }

    [Test]
    public async Task Find_WhenSearchNamesMoreTracesThanTheCap_FetchesNoMoreThanTheCapAndSaysSo()
    {
        var hits = Enumerable.Range(0, 120).Select(i => ($"t{i}", (string?)"s1")).ToList();
        var stores = new ScriptedStores((r, _) => Answer(r, id => StoreJson.Trace(id, "SHIP", "SHIP-1", "s1"), StoreJson.Search(hits), StoreJson.EmptyLoki));

        var found = await Finder(stores, new FakeTimeProvider(Now)).FindAsync(Query(), CancellationToken.None);

        stores.Count("/api/traces/").ShouldBe(FindScope.MaxTraces);
        found.Reachable.ShouldBeTrue();
        found.Problem.ShouldNotBeNull();
        found.Problem.ShouldContain(FindScope.MaxTraces.ToString());
        found.Runs.ShouldBeEmpty();
    }

    [Test]
    public async Task Find_WhenTheCallRunsPastTheDeadline_ReturnsUnreachableWithAProblem()
    {
        var time = new FakeTimeProvider(Now);
        var stores = new ScriptedStores((_, ct) =>
        {
            time.Advance(TempoLokiRunFinder.Deadline + TimeSpan.FromSeconds(1));
            ct.ThrowIfCancellationRequested();
            return ScriptedStores.Json("{}");
        });

        var found = await Finder(stores, time).FindAsync(Query(), CancellationToken.None);

        found.Reachable.ShouldBeFalse();
        found.Problem.ShouldNotBeNull();
        found.Problem.ShouldContain("20 seconds");
    }

    [Test]
    public void Clients_HaveARequestTimeoutOfFifteenSeconds()
    {
        var clients = StoreClients.Create(Options);

        clients.CreateClient(InspectionHttp.TempoClient).Timeout.ShouldBe(TimeSpan.FromSeconds(15));
        clients.CreateClient(InspectionHttp.LokiClient).Timeout.ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Test]
    public async Task Find_WhenALogNamesAnotherProjectsTrace_LeavesThatTraceOutOfTheRun()
    {
        var stores = new ScriptedStores((r, _) => Answer(
            r,
            id => id == "mine" ? StoreJson.Trace(id, "SHIP", "SHIP-1", "s1") : StoreJson.Trace(id, "OTHER", "SHIP-1", "s1"),
            StoreJson.Search([("mine", "s1")]),
            StoreJson.Loki("SHIP", "s1", "theirs")));

        var found = await Finder(stores, new FakeTimeProvider(Now)).FindAsync(Query(), CancellationToken.None);

        stores.Count("/api/traces/theirs").ShouldBeGreaterThan(0);
        found.Runs.Count.ShouldBe(1);
        found.Runs[0].SpanCount.ShouldBe(1);
    }

    [Test]
    public async Task Find_WithAKeyHoldingQuotesAndBackslashes_SendsItEscapedToBothStores()
    {
        var stores = new ScriptedStores((r, _) => Answer(r, id => StoreJson.Trace(id, "SHIP", "x", "s1"), StoreJson.Search([]), StoreJson.EmptyLoki));

        await Finder(stores, new FakeTimeProvider(Now)).FindAsync(Query("A\"B\\C"), CancellationToken.None);

        var tempo = stores.Requests.Where(r => r.RequestUri!.Host == "tempo.test").Select(r => Uri.UnescapeDataString(r.RequestUri!.Query)).ToList();
        var loki = stores.Requests.Where(r => r.RequestUri!.Host == "loki.test").Select(r => Uri.UnescapeDataString(r.RequestUri!.Query)).ToList();
        foreach (var queries in new[] { tempo, loki })
        {
            queries.ShouldContain(q => q.Contains("A\\\"B\\\\C", StringComparison.Ordinal));
            queries.ShouldContain(q => q.Contains("A\\\\\\\"B\\\\\\\\C", StringComparison.Ordinal));
        }
        stores.Requests.Count.ShouldBeGreaterThan(1);
    }
}
