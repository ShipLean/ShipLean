using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

[TestFixture]
public class EndSprintTests : SprintFixture
{
    [Test]
    public async Task EndSprint_WhenBeforeEndDate_ShouldRefuseWithoutEndToday()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);

        var reply = await CallToolAsync(token, "end_sprint", """{"project":"SHIP","sprint":"current"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation);
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("end_today");
        reply.Structured().GetProperty("error").GetProperty("message").GetString()!.ShouldContain("end_today");
    }

    [Test]
    public async Task EndSprint_ShouldStampRolledItemsWithAgentName()
    {
        await ParkBacklogAsync();
        var ended = await AddSprintAsync("Ended", -20, -6);
        var next = await AddSprintAsync("After ended", 1, 14);
        var story = await AddStoryAsync("Unfinished", 5, ended);
        var readOnly = await TokenAsync(TestUser.Joel, ReadOnly);
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);

        var preview = await CallToolAsync(readOnly, "end_sprint", """{"project":"EMPT","sprint":"Ended"}""");

        preview.IsToolError().ShouldBeFalse(preview.Body);
        Keys(preview.Structured()).ShouldBe([await KeyOfAsync(story)]);
        preview.Structured().GetProperty("items")[0].GetProperty("to").GetString().ShouldBe("After ended");
        (await SprintOfAsync(story)).ShouldBe(ended);

        var applied = await CallToolAsync(token, "end_sprint", $$"""{"project":"EMPT","plan":"{{preview.Structured().GetProperty("plan").GetString()}}"}""");

        applied.IsToolError().ShouldBeFalse(applied.Body);
        (await SprintOfAsync(story)).ShouldBe(next);
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<string>("SELECT rollover_status FROM sprint WHERE id = @ended", new { ended })).ShouldBe("decided");
        (await connection.QuerySingleAsync<string?>(
            "SELECT agent_name FROM revision WHERE work_item_id = @story ORDER BY rev DESC LIMIT 1", new { story })).ShouldBe("Test Agent (unverified)");
    }
}
