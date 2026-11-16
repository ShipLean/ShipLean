using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;
using ShipLean.WebApi.Tests.Mcp.Work;

namespace ShipLean.WebApi.Tests.Mcp.Capture;

// capture_work and refine_work_item set a Bug's Found before release flag, as Joel on SHIP.
[TestFixture]
public class FoundBeforeReleaseToolTests : McpFixture
{
    protected override bool WithProbeTools => false;

    private async Task<bool> StoredAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<bool>("SELECT found_before_release FROM work_item WHERE key = @key", new { key });
    }

    private async Task<string> CaptureAsync(string token, string item)
    {
        var reply = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","items":[{{item}}]}""");
        reply.IsToolError().ShouldBeFalse(reply.Structured().ToString());
        return reply.Structured().GetProperty("items")[0].GetProperty("key").GetString()!;
    }

    [Test]
    public async Task Capture_WhenFlagGiven_ShouldSetIt()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"bug","title":"Found in review","found_before_release":true}]}""");

        reply.IsToolError().ShouldBeFalse();
        (await StoredAsync(reply.Structured().GetProperty("items")[0].GetProperty("key").GetString()!)).ShouldBeTrue();
    }

    [Test]
    public async Task Capture_WhenFlagOnNonBug_ShouldReturnValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"story","title":"Not a bug","found_before_release":true}]}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Refine_WhenFlagGiven_ShouldSetIt()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var created = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"bug","title":"Filed after release"}]}""");
        var key = created.Structured().GetProperty("items")[0].GetProperty("key").GetString()!;
        (await StoredAsync(key)).ShouldBeFalse();

        var reply = await CallToolAsync(token, "refine_work_item", $$"""{"item":"{{key}}","found_before_release":true}""");

        reply.IsToolError().ShouldBeFalse();
        (await StoredAsync(key)).ShouldBeTrue();
    }

    // get_work_item shows a Bug's flag and leaves it out for any other type (SHIP-2040). Mutation: always send false.
    [Test]
    public async Task GetWorkItem_WhenBugFlagged_ShouldShowFoundBeforeRelease()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var bugKey = await CaptureAsync(token, """{"type":"bug","title":"Found in review","found_before_release":true}""");
        var storyKey = await CaptureAsync(token, """{"type":"story","title":"Not a bug"}""");

        var bug = await CallToolAsync(token, "get_work_item", $$"""{"key":"{{bugKey}}"}""");
        var story = await CallToolAsync(token, "get_work_item", $$"""{"key":"{{storyKey}}"}""");

        bug.Structured().GetProperty("found_before_release").GetBoolean().ShouldBeTrue();
        story.Structured().TryGetProperty("found_before_release", out _).ShouldBeFalse();
    }
}
