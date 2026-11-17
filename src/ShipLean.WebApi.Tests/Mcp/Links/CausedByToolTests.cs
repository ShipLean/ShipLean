using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Links;

// The `link` tool with type caused_by: a Bug to the Task that introduced it, and nothing else.
[TestFixture]
public class CausedByToolTests : McpFixture
{
    protected override bool WithProbeTools => false;

    private async Task<(Guid Id, string Key)> ArrangeAsync(string type)
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithTitle($"A {type} for caused by").InsertAsync(connection);
        return (id, await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }));
    }

    [Test]
    public async Task Link_WhenKindCausedBy_ShouldCreateLink()
    {
        var bug = await ArrangeAsync("bug");
        var task = await ArrangeAsync("task");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "link", $$"""{"item":"{{bug.Key}}","to":"{{task.Key}}","type":"caused_by"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("link_type").GetString().ShouldBe("caused_by");
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM work_item_link WHERE source_id = @bug AND target_id = @task AND link_type = 'causedBy' AND archived_at IS NULL", new { bug = bug.Id, task = task.Id })).ShouldBe(1);
    }

    [Test]
    public async Task Link_WhenStoryCausedByTask_ShouldReturnValidation()
    {
        var story = await ArrangeAsync("story");
        var task = await ArrangeAsync("task");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "link", $$"""{"item":"{{story.Key}}","to":"{{task.Key}}","type":"caused_by"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
    }
}
