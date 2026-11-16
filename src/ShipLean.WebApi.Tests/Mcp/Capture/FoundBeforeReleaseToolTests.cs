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
}
