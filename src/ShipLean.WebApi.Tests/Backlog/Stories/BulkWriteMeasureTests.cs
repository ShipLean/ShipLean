using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using static ShipLean.WebApi.Tests.Backlog.Stories.BulkWriteWire;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// D4: what a full 100-row action costs on the local Postgres, three runs each. The bound is the 2 seconds the plan allows; the
// numbers are printed so the report can quote them. A second writer patches an unrelated story during the first run, to see the
// project lock hold it up.
[TestFixture]
public class BulkWriteMeasureTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(2);

    [Test]
    public async Task Bulk_WhenHundredRowsMoveToSprint_ShouldFinishWithinLimit()
    {
        var runs = await MeasureAsync("sprint move", (row, run) => Item(row.Id, row.Revision, Sprint(run % 2 == 0 ? SeedIds.Sprints.Sprint26 : null), null));
        runs.ShouldAllBe(run => run < Limit);
    }

    [Test]
    public async Task Bulk_WhenHundredRowsMoveToTop_ShouldFinishWithinLimit()
    {
        var runs = await MeasureAsync("block to top", (row, _) => Item(row.Id, row.Revision, null, Slot("first")));
        runs.ShouldAllBe(run => run < Limit);
    }

    [Test]
    public async Task Bulk_WhenHundredRowsGetPoints_ShouldFinishWithinLimit()
    {
        var runs = await MeasureAsync("points", (row, run) => Item(row.Id, row.Revision, Points(run + 1), null));
        runs.ShouldAllBe(run => run < Limit);
    }

    private async Task<List<TimeSpan>> MeasureAsync(string name, Func<(Guid Id, int Revision), int, JsonObject> item)
    {
        var rows = await CreateManyAsync(Api, Project, 100, name);
        var bystander = await CreateAsync(Api, Project, $"{name} bystander");
        var delivered = Channel.CreateUnbounded<ProjectEvent>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(Project, projectEvent =>
        {
            delivered.Writer.TryWrite(projectEvent);
            return Task.CompletedTask;
        });
        var revisions = rows.ToList();
        var runs = new List<TimeSpan>();
        for (var run = 0; run < 3; run++)
        {
            var other = run == 0 ? Task.Run(() => TimedPatchAsync(bystander.Id, bystander.Revision)) : null;
            var clock = Stopwatch.StartNew();
            var body = await BulkAsync(Api, Project, [.. revisions.Select(r => item(r, run))]);
            clock.Stop();
            body.GetProperty("stale").GetArrayLength().ShouldBe(0);
            revisions = [.. body.GetProperty("written").EnumerateArray().Select(w => (w.GetProperty("id").GetGuid(), w.GetProperty("revision").GetInt32()))];
            var bystanderMs = other is null ? "" : $", concurrent PATCH of another story {(await other).TotalMilliseconds:F0} ms";
            var published = await CountEventsAsync(delivered.Reader, other is null ? 100 : 101);
            await TestContext.Out.WriteLineAsync($"MEASURE {name} run {run + 1}: {clock.ElapsedMilliseconds} ms for 100 rows, {published} events{bystanderMs}");
            runs.Add(clock.Elapsed);
        }

        return runs;
    }

    // Reads until the expected number of events has arrived, which also proves none is missing; extras after it are caught by the next run's count.
    private static async Task<int> CountEventsAsync(ChannelReader<ProjectEvent> reader, int expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var count = 0;
        while (count < expected)
        {
            await reader.ReadAsync(timeout.Token);
            count++;
        }

        return count;
    }

    private async Task<TimeSpan> TimedPatchAsync(Guid id, int revision)
    {
        var clock = Stopwatch.StartNew();
        using var response = await Api.PatchJsonAsync($"api/work-items/{id}", new JsonObject { ["expectedRevision"] = revision, ["changes"] = new JsonObject { ["title"] = new JsonObject { ["value"] = "Bystander" } } });
        response.EnsureSuccessStatusCode();
        return clock.Elapsed;
    }
}
