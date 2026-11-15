using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse.Read;

// GetRun reads the run's session, then the rest of its traces, inside the project: nothing of another project or session comes back.
[TestFixture(TestName = "ClickHouse GetRun scoping, SHIP-1557")]
[Category("SHIP-1557")]
public sealed class ClickHouseRunScopingTests
{
    [Test]
    public async Task GetRun_WhenATraceHoldsASessionlessSpanAndAnotherProjectsSpan_KeepsTheFirstAndExcludesTheSecond()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await Span(scratch, "mine", "SCR", "s1", 0);
        await Span(scratch, "child-without-session", "SCR", null, 1);
        await Span(scratch, "other-project", "OTHER", null, 2);
        await Span(scratch, "other-project-session", "OTHER", "s1", 3);
        await Span(scratch, "other-trace", "SCR", null, 4, "trace-2");

        var run = await Read(scratch, "s1");

        run.ShouldNotBeNull();
        run.Spans.Select(s => s.Name).ShouldBe(["mine", "child-without-session"]);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task GetRun_WhenTheRunIdIsBlank_IsNull(string runId)
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await Span(scratch, "mine", "SCR", "s1", 0);
        await Span(scratch, "sessionless", "SCR", null, 1, "trace-2");

        (await Read(scratch, runId)).ShouldBeNull();
    }

    private static async Task<RawRun?> Read(ScratchExporter scratch, string runId)
    {
        var migrator = scratch.Migrator();
        var reader = new ClickHouseRunReader(scratch.Options, migrator, scratch.Finder(migrator));
        var query = ScratchExporter.Query("SHIP-1");
        return await reader.GetAsync(new RunRef(query.ProjectKey, "SHIP-1", runId, query.From, query.To, null, null), CancellationToken.None);
    }

    private static Task Span(ScratchExporter scratch, string name, string project, string? session, int seconds, string trace = "trace-1")
    {
        var attributes = new Dictionary<string, string>();
        if (session is not null)
        {
            attributes["session.id"] = session;
        }
        if (project == "SCR" && session == "s1")
        {
            attributes["shiplean.item"] = "SHIP-1";
        }
        return scratch.Db.ExecuteAsync(
            $$"""
            INSERT INTO {{scratch.ExporterDatabase}}.otel_traces (Timestamp, TraceId, SpanId, SpanName, ServiceName, ResourceAttributes, SpanAttributes, Duration, StatusCode)
            VALUES ({at:DateTime64(9, 'UTC')}, {trace:String}, {id:String}, {name:String}, 'claude-code', {resource:Map(String, String)}, {attributes:Map(String, String)}, 1000000, 'Unset')
            """,
            new Dictionary<string, object>
            {
                ["at"] = ScratchExporter.Query("SHIP-1").From.UtcDateTime.AddMinutes(1).AddSeconds(seconds),
                ["trace"] = trace,
                ["id"] = name,
                ["name"] = name,
                ["resource"] = new Dictionary<string, string> { ["shiplean.project"] = project },
                ["attributes"] = attributes,
            },
            CancellationToken.None);
    }
}
