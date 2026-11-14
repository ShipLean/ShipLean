using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Timeline;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Reviews;

// log_review as Joel on SHIP: one task per test, so the rounds are counted from the first.
[TestFixture]
public class LogReviewToolTests : McpFixture
{
    private const string Agent = "Test Agent (unverified)";

    protected override bool WithProbeTools => false;

    private async Task<(Guid Id, string Key)> NewItemAsync(string type)
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithTitle($"Review {type}").InsertAsync(connection);
        return (id, await TimelineTestKeys.KeyAsync(connection, id));
    }

    private async Task<List<(string Verdict, string? Note, Guid ByUser, string? Agent)>> RowsAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return (await connection.QueryAsync<(string, string?, Guid, string?)>(
            "SELECT verdict, note, by_user_id, agent_name FROM review_event WHERE work_item_id = @id ORDER BY at, id", new { id })).ToList();
    }

    [Test]
    public async Task LogReview_WhenTask_ShouldAppendEvent()
    {
        var (id, key) = await NewItemAsync("task");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var first = await CallToolAsync(token, "log_review", $$"""{"item":"{{key}}","verdict":"changes_requested","note":"Missing a test."}""");
        var second = await CallToolAsync(token, "log_review", $$"""{"item":"{{key}}","verdict":"approved"}""");

        first.IsToolError().ShouldBeFalse();
        first.Structured().GetProperty("round").GetInt32().ShouldBe(1);
        first.Structured().GetProperty("verdict").GetString().ShouldBe("changes_requested");
        first.Structured().GetProperty("key").GetString().ShouldBe(key);
        first.Structured().GetProperty("project").GetString().ShouldBe("SHIP");
        first.ResultText().ShouldBe($"Review logged on {key}: changes_requested (round 1)");
        second.Structured().GetProperty("round").GetInt32().ShouldBe(2);
        second.ResultText().ShouldBe($"Review logged on {key}: approved (round 2)");
        var rows = await RowsAsync(id);
        rows.Select(r => r.Verdict).ShouldBe(["changesRequested", "approved"]);
        rows[0].Note.ShouldBe("Missing a test.");
        rows.ShouldAllBe(r => r.ByUser == TestUser.Joel.Id);
    }

    [Test]
    public async Task LogReview_WhenStory_ShouldReturnValidation()
    {
        var (id, key) = await NewItemAsync("story");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "log_review", $$"""{"item":"{{key}}","verdict":"approved"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("message").GetString().ShouldContain("task");
        (await RowsAsync(id)).ShouldBeEmpty();
    }

    [Test]
    public async Task LogReview_WhenVerdictUnknown_ShouldReturnValidation()
    {
        var (id, key) = await NewItemAsync("task");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "log_review", $$"""{"item":"{{key}}","verdict":"changesRequested"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        (await RowsAsync(id)).ShouldBeEmpty();
    }

    [Test]
    public async Task LogReview_WhenAgent_ShouldStampAgentName()
    {
        var (id, key) = await NewItemAsync("task");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        await CallToolAsync(token, "log_review", $$"""{"item":"{{key}}","verdict":"approved"}""");

        (await RowsAsync(id)).Single().Agent.ShouldBe(Agent);
    }
}
