using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Read;

// GetRunAsync in Tempo/Loki mode: Tempo's v2 trace shape, the K3 link check, truncation and a store that fails.
[TestFixture(TestName = "Tempo and Loki run reader")]
public sealed class TempoLokiRunReaderTests
{
    private const string OneHit = "{\"traces\":[{\"traceID\":\"t1\"}]}";

    [Test]
    public async Task Get_WhenTempoWritesBase64Ids_ReturnsHexIdsWithTheParentOfTheChild()
    {
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace(), OneHit, StoreJson.EmptyLoki);

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        run.ShouldNotBeNull();
        run.Spans.Select(s => s.SpanId).ShouldBe(["0000000000001001", "0000000000001002"]);
        run.Spans.Single(s => s.Name == "root").ParentSpanId.ShouldBeNull();
        run.Spans.Single(s => s.Name == "child").ParentSpanId.ShouldBe("0000000000001001");
        run.Spans.Single(s => s.Name == "child").Kind.ShouldBe("client");
        run.Spans.Single(s => s.Name == "child").Status.ShouldBe(SpanStatus.Error);
        run.Spans.Single(s => s.Name == "child").StatusMessage.ShouldBe("boom");
        run.Spans.ShouldAllBe(s => s.TraceId == "t1");
        run.Truncated.ShouldBeFalse();
    }

    [Test]
    public async Task Get_WhenTheRunLinksToAnotherItemOnly_IsNull()
    {
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace(item: "SHIP-2"), OneHit, StoreJson.EmptyLoki);

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref("SHIP-1"), CancellationToken.None);

        run.ShouldBeNull();
    }

    [Test]
    public async Task Get_WhenLokiReturnsItsLimit_IsTruncated()
    {
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace(), "{\"traces\":[]}", ReaderStores.LokiLines(5000));

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        run.ShouldNotBeNull();
        run.Truncated.ShouldBeTrue();
        run.Logs.Count.ShouldBe(5000);
    }

    [Test]
    public async Task Get_WhenTempoSaysTheTraceIsPartial_IsTruncated()
    {
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace(status: "partial"), OneHit, StoreJson.EmptyLoki);

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        run.ShouldNotBeNull();
        run.Truncated.ShouldBeTrue();
    }

    [Test]
    public async Task Get_WhenSearchNamesMoreTracesThanTheCap_FetchesTheCapAndIsTruncated()
    {
        var hits = "{\"traces\":[" + string.Join(",", Enumerable.Range(0, 80).Select(i => $"{{\"traceID\":\"t{i}\"}}")) + "]}";
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace(), hits, StoreJson.EmptyLoki);

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        run.ShouldNotBeNull();
        run.Truncated.ShouldBeTrue();
        stores.Count("/api/v2/traces/").ShouldBe(50);
    }

    [Test]
    public async Task Get_WhenALogNamesATraceTheSearchMissed_FetchesItById()
    {
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace(), "{\"traces\":[]}", ReaderStores.LokiLines(1, traceId: "lagging"));

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        run.ShouldNotBeNull();
        run.Spans.Count.ShouldBe(2);
        stores.Requests.Any(r => r.RequestUri!.AbsolutePath.EndsWith("/api/v2/traces/lagging", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Test]
    public async Task Get_WhenTheTraceBelongsToAnotherProject_IsNull()
    {
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace(project: "OTHER"), OneHit, StoreJson.EmptyLoki);

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        run.ShouldBeNull();
    }

    [TestCase("timeout")]
    [TestCase("notfound")]
    [TestCase("malformed")]
    [TestCase("wrongshape")]
    public async Task Get_WhenTheStoreFails_IsNullAndDoesNotThrow(string kind)
    {
        var run = await ReaderStores.Reader(ReaderStores.Failing(kind)).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        run.ShouldBeNull();
    }

    [TestCase("")]
    [TestCase("  ")]
    public async Task Get_WhenTheRunIdIsBlank_IsNullAndAsksNoStore(string runId)
    {
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace(session: ""), OneHit, ReaderStores.LokiLines(2, session: ""));

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref(run: runId), CancellationToken.None);

        run.ShouldBeNull();
        stores.Requests.ShouldBeEmpty();
    }

    [TestCase("\"kind\":\"SPAN_KIND_CLIENT\"", "\"kind\":{}")]
    [TestCase("\"code\":\"STATUS_CODE_ERROR\"", "\"code\":[]")]
    [TestCase("\"code\":\"STATUS_CODE_ERROR\"", "\"code\":1.5")]
    [TestCase("\"status\":{}}", "\"status\":{},\"attributes\":[{\"key\":\"x\",\"value\":{\"boolValue\":\"yes\"}}]}")]
    [TestCase("\"status\":{}}", "\"status\":{},\"attributes\":[{\"key\":\"x\",\"value\":{\"intValue\":{}}}]}")]
    [TestCase("\"status\":{}}", "\"status\":{},\"attributes\":[{\"key\":\"x\",\"value\":{\"doubleValue\":\"1\"}}]}")]
    [TestCase("\"status\":{}}", "\"status\":{},\"attributes\":{}}")]
    [TestCase("\"status\":{}}", "\"status\":{},\"attributes\":[{\"key\":\"input_tokens\",\"value\":{\"stringValue\":\"12.5\"}}]}")]
    [TestCase("\"scopeSpans\":[{\"spans\":[", "\"scopeSpans\":[{\"spans\":{},\"x\":[")]
    public async Task Get_WhenABodyHasAValueOfTheWrongKind_IsNullAndDoesNotThrow(string find, string replace)
    {
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace().Replace(find, replace, StringComparison.Ordinal), OneHit, StoreJson.EmptyLoki);

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        run.ShouldBeNull();
    }

    [Test]
    public async Task Get_WhenATraceHasSpansOutsideTheWindow_AsksWithTheWindowAndDropsThem()
    {
        var old = "\"status\":{}},{\"spanId\":\"AAAAAAAAEAM=\",\"name\":\"old\",\"startTimeUnixNano\":\"1700000000000000000\",\"endTimeUnixNano\":\"1700000001000000000\",\"status\":{}},";
        var stores = ReaderStores.Stores(_ => ReaderStores.Trace().Replace("\"status\":{}},", old, StringComparison.Ordinal), OneHit, StoreJson.EmptyLoki);

        var run = await ReaderStores.Reader(stores).GetAsync(ReaderStores.Ref(), CancellationToken.None);

        run.ShouldNotBeNull();
        run.Spans.Select(s => s.Name).ShouldBe(["root", "child"]);
        var fetch = stores.Requests.Single(r => r.RequestUri!.AbsolutePath.Contains("/api/v2/traces/", StringComparison.Ordinal)).RequestUri!.Query;
        fetch.ShouldContain($"start={ReaderStores.Now.AddDays(-1).ToUnixTimeSeconds()}");
        fetch.ShouldContain($"end={ReaderStores.Now.ToUnixTimeSeconds() + 1}");
    }
}
