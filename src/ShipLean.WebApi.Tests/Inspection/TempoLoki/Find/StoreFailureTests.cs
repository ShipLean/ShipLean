using System.Net;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

// K3: whatever a store does (no answer in time, 404 from a proxy, a body that is not what it should be), the port answers with
// Reachable=false or a field that is not found, and never throws.
[TestFixture(TestName = "Tempo and Loki store failures")]
public sealed class StoreFailureTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly InspectionOptions Options = new() { TempoUrl = "http://tempo.test", LokiUrl = "http://loki.test" };

    private static ScriptedStores Failing(string kind) => new((_, _) => kind switch
    {
        "timeout" => throw new TaskCanceledException("The request timed out."),
        "notfound" => ScriptedStores.Json("{}", HttpStatusCode.NotFound),
        "malformed" => ScriptedStores.Json("{nope"),
        _ => ScriptedStores.Json("[]"),
    });

    [TestCase("timeout")]
    [TestCase("notfound")]
    [TestCase("malformed")]
    [TestCase("wrongshape")]
    public async Task Find_WhenTheStoreFails_IsUnreachableWithAProblem(string kind)
    {
        var clients = StoreClients.Create(Options, Failing(kind));
        var time = new FakeTimeProvider(Now);
        var finder = new TempoLokiRunFinder(new TempoRunFinder(clients, Options), new LokiRunFinder(clients, Options), new RunLinkCache(new InMemoryRunLinkStore(), time), Options, time);

        var found = await finder.FindAsync(new RunQuery("SHIP", "SHIP-1", Now.AddDays(-1), Now, null, null), CancellationToken.None);

        found.Reachable.ShouldBeFalse();
        found.Problem.ShouldNotBeNull();
    }

    [TestCase("{\"traces\":[{\"traceID\":\"t1\",\"spanSets\":[{\"spans\":[{\"attributes\":[{\"key\":\"session.id\",\"value\":{\"boolValue\":\"yes\"}}]}]}]}]}")]
    [TestCase("{\"traces\":[{\"traceID\":\"t1\",\"spanSets\":[{\"spans\":[{\"attributes\":{}}]}]}]}")]
    [TestCase("{\"traces\":[{\"traceID\":\"t1\",\"spanSets\":[{\"spans\":[{\"attributes\":[{\"key\":\"x\",\"value\":{\"intValue\":{}}}]}]}]}]}")]
    public async Task Find_WhenASearchAnswerHasAValueOfTheWrongKind_IsUnreachableAndDoesNotThrow(string search)
    {
        var stores = new ScriptedStores((request, _) => ScriptedStores.Json(request.RequestUri!.AbsolutePath.EndsWith("/api/search", StringComparison.Ordinal) ? search : StoreJson.EmptyLoki));
        var clients = StoreClients.Create(Options, stores);
        var time = new FakeTimeProvider(Now);
        var finder = new TempoLokiRunFinder(new TempoRunFinder(clients, Options), new LokiRunFinder(clients, Options), new RunLinkCache(new InMemoryRunLinkStore(), time), Options, time);

        var found = await finder.FindAsync(new RunQuery("SHIP", "SHIP-1", Now.AddDays(-1), Now, null, null), CancellationToken.None);

        found.Reachable.ShouldBeFalse();
    }

    [TestCase("timeout")]
    [TestCase("notfound")]
    public async Task Health_WhenTheStoreFails_IsUnreachable(string kind)
    {
        var health = new TempoLokiHealth(StoreClients.Create(Options, Failing(kind)), Options, new FakeTimeProvider(Now));

        var result = await health.CheckAsync(CancellationToken.None);

        result.Reachable.ShouldBeFalse();
        result.Problem.ShouldNotBeNull();
    }

    [TestCase("timeout")]
    [TestCase("notfound")]
    [TestCase("malformed")]
    [TestCase("wrongshape")]
    public async Task CheckFields_WhenTheStoreFails_FindsNoFieldAndDoesNotThrow(string kind)
    {
        var check = new TempoLokiFieldCheck(StoreClients.Create(Options, Failing(kind)), Options, new FakeTimeProvider(Now));

        var fields = await check.CheckAsync(CancellationToken.None);

        fields.ShouldNotBeEmpty();
        fields.ShouldAllBe(f => !f.Found);
    }
}
