using ShipLean.Contracts.Backlog;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

// plan_sprint at the row cap (BulkLimits.MaxRows): one apply moves all of the rows or none of them.
[TestFixture]
public class PlanSprintLimitTests : SprintFixture
{
    private static string Preview(string sprint) => $$"""{"project":"EMPT","sprint":"{{sprint}}"}""";

    private static string Apply(string plan) => $$"""{"project":"EMPT","plan":"{{plan}}"}""";

    [Test]
    public async Task PlanSprint_When100Fit_ShouldMoveThemAll()
    {
        await ParkBacklogAsync();
        var sprint = await AddSprintAsync("Hundred", 1, 14, capacity: 1000);
        await AddStoriesAsync("Hundred row", BulkLimits.MaxRows, 1m);
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);

        var preview = await CallToolAsync(token, "plan_sprint", Preview("Hundred"));

        Keys(preview.Structured()).Length.ShouldBe(BulkLimits.MaxRows);
        preview.Structured().GetProperty("warnings").EnumerateArray().ShouldNotContain(w => w.GetString()!.StartsWith("Stopped at"));
        var applied = await CallToolAsync(token, "plan_sprint", Apply(preview.Structured().GetProperty("plan").GetString()!));

        applied.IsToolError().ShouldBeFalse(applied.Body);
        Keys(applied.Structured()).Length.ShouldBe(BulkLimits.MaxRows);
        (await InSprintAsync(sprint)).ShouldBe(BulkLimits.MaxRows);
    }

    [Test]
    public async Task PlanSprint_WhenMoreThan100Fit_ShouldWarnAndStopAt100()
    {
        await ParkBacklogAsync();
        var sprint = await AddSprintAsync("Hundred and one", 1, 14, capacity: 1000);
        await AddStoriesAsync("Over row", BulkLimits.MaxRows + 1, 1m);
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);

        var preview = await CallToolAsync(token, "plan_sprint", Preview("Hundred and one"));

        Keys(preview.Structured()).Length.ShouldBe(BulkLimits.MaxRows);
        preview.Structured().GetProperty("warnings").EnumerateArray().ShouldContain(w => w.GetString()!.Contains($"Stopped at {BulkLimits.MaxRows} items"));
        var applied = await CallToolAsync(token, "plan_sprint", Apply(preview.Structured().GetProperty("plan").GetString()!));

        applied.IsToolError().ShouldBeFalse(applied.Body);
        (await InSprintAsync(sprint)).ShouldBe(BulkLimits.MaxRows);
    }

    [Test]
    public async Task PlanSprint_WhenOneRowStale_ShouldMoveNothing()
    {
        await ParkBacklogAsync();
        var sprint = await AddSprintAsync("Stale hundred", 1, 14, capacity: 1000);
        var rows = await AddStoriesAsync("Stale row", BulkLimits.MaxRows, 1m);
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var plan = (await CallToolAsync(token, "plan_sprint", Preview("Stale hundred"))).Structured().GetProperty("plan").GetString()!;
        await using (var connection = await OpenDatabaseAsync())
        {
            await Dapper.SqlMapper.ExecuteAsync(connection, "UPDATE work_item SET revision = revision + 1 WHERE id = @last", new { last = rows[^1] });
        }

        var reply = await CallToolAsync(token, "plan_sprint", Apply(plan));

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Conflict);
        (await InSprintAsync(sprint)).ShouldBe(0);
    }
}
