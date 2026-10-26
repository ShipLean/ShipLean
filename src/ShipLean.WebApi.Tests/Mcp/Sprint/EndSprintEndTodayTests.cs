using Dapper;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

[TestFixture]
public class EndSprintEndTodayTests : SprintFixture
{
    [Test]
    public async Task EndSprint_WhenEndToday_ShouldMoveEndDateAndRollOver()
    {
        await ParkBacklogAsync();
        var running = await AddSprintAsync("Running", -5, 5);
        var next = await AddSprintAsync("After running", 6, 19);
        var story = await AddStoryAsync("Carried", 3, running);
        var readOnly = await TokenAsync(TestUser.Joel, ReadOnly);
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);

        var preview = await CallToolAsync(readOnly, "end_sprint", """{"project":"EMPT","sprint":"Running","end_today":true}""");

        preview.IsToolError().ShouldBeFalse(preview.Body);
        preview.Structured().GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldContain(w => w!.Contains("end date will move"));
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<DateOnly>("SELECT end_date FROM sprint WHERE id = @running", new { running })).ShouldBe(Today.AddDays(5));

        var applied = await CallToolAsync(token, "end_sprint", $$"""{"project":"EMPT","plan":"{{preview.Structured().GetProperty("plan").GetString()}}"}""");

        applied.IsToolError().ShouldBeFalse(applied.Body);
        (await connection.QuerySingleAsync<DateOnly>("SELECT end_date FROM sprint WHERE id = @running", new { running })).ShouldBe(Today);
        (await connection.QuerySingleAsync<string>("SELECT rollover_status FROM sprint WHERE id = @running", new { running })).ShouldBe("decided");
        (await SprintOfAsync(story)).ShouldBe(next);
    }
}
