using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

[TestFixture]
public class EndSprintNextChangedTests : SprintFixture
{
    [Test]
    public async Task EndSprint_WhenNextSprintAppearedSincePreview_ShouldBeConflict()
    {
        await ParkBacklogAsync();
        var ended = await AddSprintAsync("Finished", -20, -6);
        var story = await AddStoryAsync("Stays put", 3, ended);
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var preview = await CallToolAsync(token, "end_sprint", """{"project":"EMPT","sprint":"Finished"}""");
        preview.IsToolError().ShouldBeFalse(preview.Body);
        var created = await CallToolAsync(token, "create_sprint", """{"project":"EMPT"}""");
        created.IsToolError().ShouldBeFalse(created.Body);

        var reply = await CallToolAsync(token, "end_sprint", $$"""{"project":"EMPT","plan":"{{preview.Structured().GetProperty("plan").GetString()}}"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Conflict);
        (await SprintOfAsync(story)).ShouldBe(ended);
    }
}
