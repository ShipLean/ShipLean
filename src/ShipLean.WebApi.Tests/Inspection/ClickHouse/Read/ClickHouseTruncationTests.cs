using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse.Read;

// The reader's caps: 5,000 spans and 10,000 logs, with Truncated set only when a run is over one of them.
[TestFixture(TestName = "ClickHouse GetRun truncation, SHIP-1557")]
[Category("SHIP-1557")]
public sealed class ClickHouseTruncationTests
{
    private const string FullTraceColumns = """
        Timestamp DateTime64(9), TraceId String, SpanId String, ParentSpanId String, SpanName LowCardinality(String), SpanKind LowCardinality(String),
        ServiceName LowCardinality(String), ResourceAttributes Map(LowCardinality(String), String), SpanAttributes Map(LowCardinality(String), String),
        Duration UInt64, StatusCode LowCardinality(String), StatusMessage String,
        `Events.Timestamp` Array(DateTime64(9)), `Events.Name` Array(LowCardinality(String)), `Events.Attributes` Array(Map(LowCardinality(String), String))
        """;

    [TestCase(ClickHouseRunReader.MaxSpans, 0, false)]
    [TestCase(ClickHouseRunReader.MaxSpans + 1, 0, true)]
    [TestCase(1, ClickHouseRunReader.MaxLogs, false)]
    [TestCase(1, ClickHouseRunReader.MaxLogs + 1, true)]
    public async Task GetRun_WhenTheRunIsOverACap_IsCutAndMarkedTruncated(int spans, int logs, bool truncated)
    {
        await using var scratch = await ScratchExporter.CreateAsync(FullTraceColumns);
        await scratch.Db.ExecuteAsync($"ALTER TABLE {scratch.ExporterDatabase}.otel_logs ADD COLUMN Body String, ADD COLUMN TraceId String, ADD COLUMN SpanId String, ADD COLUMN EventName String", null, CancellationToken.None);
        await Fill(scratch, spans, logs);
        var migrator = scratch.Migrator();
        var finder = scratch.Finder(migrator);
        var reader = new ClickHouseRunReader(scratch.Options, migrator, finder);
        var query = ScratchExporter.Query("SHIP-1");

        var run = await reader.GetAsync(new RunRef(query.ProjectKey, "SHIP-1", "s1", query.From, query.To, null, null), CancellationToken.None);

        run.ShouldNotBeNull();
        run.Truncated.ShouldBe(truncated);
        run.Spans.Count.ShouldBe(Math.Min(spans, ClickHouseRunReader.MaxSpans));
        run.Logs.Count.ShouldBe(Math.Min(logs, ClickHouseRunReader.MaxLogs));
    }

    [Test]
    public async Task GetRun_WhenTheRunIsCut_KeepsTheEarliestRows()
    {
        await using var scratch = await ScratchExporter.CreateAsync(FullTraceColumns);
        await scratch.Db.ExecuteAsync($"ALTER TABLE {scratch.ExporterDatabase}.otel_logs ADD COLUMN Body String, ADD COLUMN TraceId String, ADD COLUMN SpanId String, ADD COLUMN EventName String", null, CancellationToken.None);
        await Fill(scratch, ClickHouseRunReader.MaxSpans + 10, 0);
        var migrator = scratch.Migrator();
        var reader = new ClickHouseRunReader(scratch.Options, migrator, scratch.Finder(migrator));
        var query = ScratchExporter.Query("SHIP-1");

        var run = await reader.GetAsync(new RunRef(query.ProjectKey, "SHIP-1", "s1", query.From, query.To, null, null), CancellationToken.None);

        run.ShouldNotBeNull();
        run.Spans.Select(s => s.SpanId).ShouldContain("span-0");
        run.Spans.Select(s => s.SpanId).ShouldNotContain($"span-{ClickHouseRunReader.MaxSpans + 9}");
    }

    // One tagged run "s1": `spans` spans one millisecond apart from the origin, and `logs` logs after them.
    private static async Task Fill(ScratchExporter scratch, int spans, int logs)
    {
        var parameters = new Dictionary<string, object> { ["spans"] = (ulong)spans, ["logs"] = (ulong)logs };
        await scratch.Db.ExecuteAsync(
            $$"""
            INSERT INTO {{scratch.ExporterDatabase}}.otel_traces
                (Timestamp, TraceId, SpanId, ParentSpanId, SpanName, SpanKind, ServiceName, ResourceAttributes, SpanAttributes, Duration, StatusCode, StatusMessage)
            SELECT toDateTime64('2026-11-10 10:00:00', 9, 'UTC') + toIntervalMillisecond(number), 'trace-1', concat('span-', toString(number)), '', 'claude_code.tool', 'Internal',
                'claude-code', map('shiplean.project', 'SCR'), map('session.id', 's1', 'shiplean.item', 'SHIP-1'), 1000000, 'Unset', ''
            FROM numbers({spans:UInt64})
            """,
            parameters,
            CancellationToken.None);
        await scratch.Db.ExecuteAsync(
            $$"""
            INSERT INTO {{scratch.ExporterDatabase}}.otel_logs
                (Timestamp, ServiceName, SeverityText, SeverityNumber, ResourceAttributes, LogAttributes, Body, TraceId, SpanId, EventName)
            SELECT toDateTime64('2026-11-10 10:10:00', 9, 'UTC') + toIntervalMillisecond(number), 'claude-code', 'INFO', 9,
                map('shiplean.project', 'SCR'), map('session.id', 's1', 'shiplean.item', 'SHIP-1'), concat('log-', toString(number)), '', '', ''
            FROM numbers({logs:UInt64})
            """,
            parameters,
            CancellationToken.None);
    }
}
