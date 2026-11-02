using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dapper;
using ShipLean.Contracts.Backlog;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Sprint;

namespace ShipLean.WebApi.Tests.Mcp.Capture;

// capture_work, break_down and plan_sprint at the row cap (BulkLimits.MaxRows): all of the rows or none, and what one apply costs.
// The Measure tests print milliseconds and bytes for three runs each; they are the numbers SHIP-255's limit was set against.
[TestFixture]
public class CaptureMeasureTests : SprintFixture
{
    private const int Runs = 3;

    private static string Items(int count, string prefix)
        => "[" + string.Join(",", Enumerable.Range(1, count).Select(i => $$"""{"type":"task","title":"{{prefix}} {{i}}","description":"A task made at the row cap.","points":1,"tags":["agent"]}""")) + "]";

    private static string Children(int count, string prefix)
        => "[" + string.Join(",", Enumerable.Range(1, count).Select(i => $$"""{"title":"{{prefix}} {{i}}","points":1,"description":"A child made at the row cap."}""")) + "]";

    private async Task<int> CountLikeAsync(string prefix)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item WHERE title LIKE @like", new { like = prefix + " %" });
    }

    private async Task<string> ArrangeParentAsync(string title)
    {
        await using var connection = await OpenDatabaseAsync();
        var parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle(title).InsertAsync(connection);
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @parent", new { parent });
    }

    [Test]
    public async Task CaptureWork_When100Items_ShouldCreateThemAll()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var preview = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","items":{{Items(BulkLimits.MaxRows, "Cap hundred")}}}""");

        var applied = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","plan":"{{preview.Structured().GetProperty("plan").GetString()}}"}""");

        applied.IsToolError().ShouldBeFalse(applied.Body);
        applied.Structured().GetProperty("items").GetArrayLength().ShouldBe(BulkLimits.MaxRows);
        (await CountLikeAsync("Cap hundred")).ShouldBe(BulkLimits.MaxRows);
    }

    [Test]
    public async Task CaptureWork_When101Items_ShouldAnswerValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","items":{{Items(BulkLimits.MaxRows + 1, "Cap over")}}}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation);
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("items");
        reply.Structured().GetProperty("error").GetProperty("message").GetString()!.ShouldContain(BulkLimits.MaxRows.ToString());
        (await CountLikeAsync("Cap over")).ShouldBe(0);
    }

    [Test]
    public async Task BreakDown_When100Children_ShouldCreateThemAll()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var parent = await ArrangeParentAsync("Break into a hundred");

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{parent}}","children":{{Children(BulkLimits.MaxRows, "Child hundred")}}}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("items").GetArrayLength().ShouldBe(BulkLimits.MaxRows);
        (await CountLikeAsync("Child hundred")).ShouldBe(BulkLimits.MaxRows);
    }

    [Test]
    public async Task BreakDown_When101Children_ShouldAnswerValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var parent = await ArrangeParentAsync("Break into too many");

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{parent}}","children":{{Children(BulkLimits.MaxRows + 1, "Child over")}}}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation);
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("children");
        reply.Structured().GetProperty("error").GetProperty("message").GetString()!.ShouldContain(BulkLimits.MaxRows.ToString());
        (await CountLikeAsync("Child over")).ShouldBe(0);
    }

    [Test]
    public async Task Measure_WhenCaptureWorkAt100_ShouldReportMillisecondsAndBytes()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        for (var run = 1; run <= Runs; run++)
        {
            var preview = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","items":{{Items(BulkLimits.MaxRows, $"Measure capture {run}")}}}""");
            var watch = Stopwatch.StartNew();
            var applied = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","plan":"{{preview.Structured().GetProperty("plan").GetString()}}"}""");
            watch.Stop();

            applied.IsToolError().ShouldBeFalse(applied.Body);
            Report("capture_work", run, watch, preview.Body, applied.Body);
        }
    }

    [Test]
    public async Task Measure_WhenBreakDownAt100_ShouldReportMillisecondsAndBytes()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        for (var run = 1; run <= Runs; run++)
        {
            var parent = await ArrangeParentAsync($"Measure parent {run}");
            var watch = Stopwatch.StartNew();
            var applied = await CallToolAsync(token, "break_down", $$"""{"parent":"{{parent}}","children":{{Children(BulkLimits.MaxRows, $"Measure child {run}")}}}""");
            watch.Stop();

            applied.IsToolError().ShouldBeFalse(applied.Body);
            Report("break_down", run, watch, null, applied.Body);
        }
    }

    [Test]
    public async Task Measure_WhenPlanSprintAt100_ShouldReportMillisecondsAndBytes()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        for (var run = 1; run <= Runs; run++)
        {
            await ParkBacklogAsync();
            var name = $"Measure sprint {run}";
            await AddSprintAsync(name, 1, 14, capacity: 1000);
            await AddStoriesAsync($"Measure story {run}", BulkLimits.MaxRows, 1m);
            var preview = await CallToolAsync(token, "plan_sprint", $$"""{"project":"EMPT","sprint":"{{name}}"}""");
            var watch = Stopwatch.StartNew();
            var applied = await CallToolAsync(token, "plan_sprint", $$"""{"project":"EMPT","plan":"{{preview.Structured().GetProperty("plan").GetString()}}"}""");
            watch.Stop();

            applied.IsToolError().ShouldBeFalse(applied.Body);
            Keys(applied.Structured()).Length.ShouldBe(BulkLimits.MaxRows);
            Report("plan_sprint", run, watch, preview.Body, applied.Body);
        }
    }

    private static void Report(string tool, int run, Stopwatch apply, string? preview, string answer)
        => TestContext.Out.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"MEASURE {tool} run {run}: apply {apply.ElapsedMilliseconds} ms, preview {(preview is null ? "none" : Encoding.UTF8.GetByteCount(preview))} bytes, answer {Encoding.UTF8.GetByteCount(answer)} bytes"));
}
