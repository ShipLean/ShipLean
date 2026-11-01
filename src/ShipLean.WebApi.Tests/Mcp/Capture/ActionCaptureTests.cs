using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;
using ShipLean.WebApi.Tests.Mcp.Work;

namespace ShipLean.WebApi.Tests.Mcp.Capture;

// The action item type through the MCP tools, as Joel on SHIP: SHIP-104 is the seeded Feature of SHIP-89.
[TestFixture]
public class ActionCaptureTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public async Task Capture_WhenTypeIsActionUnderFeature_ShouldCreateOne()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"action","title":"Capture an action item","parent":"SHIP-104","points":2}]}""");

        reply.IsToolError().ShouldBeFalse();
        var created = reply.Structured().GetProperty("items")[0];
        created.GetProperty("type").GetString().ShouldBe("action");
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string Type, string ParentType, string State)>(
            "SELECT w.type, w.parent_type, s.name FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.key = @key", new { key = created.GetProperty("key").GetString() });
        row.ShouldBe(("action", "feature", "New"));
    }

    [Test]
    public async Task Capture_WhenTypeIsActionWithSpaces_ShouldCreateAnAction()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"Action item","title":"Spelled out action item","parent":"SHIP-104"}]}""");

        reply.IsToolError().ShouldBeFalse();
        reply.Structured().GetProperty("items")[0].GetProperty("type").GetString().ShouldBe("action");
    }

    [Test]
    public async Task Capture_WhenTypeUnknown_ShouldListActionInTheError()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"chore","title":"Nope"}]}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("message").GetString()!.ShouldContain("action");
    }

    [Test]
    public async Task BreakDown_WhenParentIsAction_ShouldCreateTasks()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "break_down", """{"parent":"SHIP-89","children":[{"title":"First step of the action"}]}""");

        reply.IsToolError().ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM work_item WHERE title = 'First step of the action' AND type = 'task' AND parent_type = 'action'")).ShouldBe(1);
    }

    [Test]
    public async Task FeatureProgress_WhenFeatureHasActions_ShouldCountThemAndTheirPoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Progress feature").InsertAsync(connection);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Open action").WithParent(feature).WithPoints(2).InsertAsync(connection);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Closed action").WithParent(feature).WithPoints(3).WithStateNamed("Closed").InsertAsync(connection);
        var key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var progress = (await CallToolAsync(token, "feature_progress", $$"""{"item":"{{key}}"}""")).Structured();

        var action = progress.GetProperty("by_type").EnumerateArray().Single(t => t.GetProperty("type").GetString() == "action");
        (action.GetProperty("open").GetInt32(), action.GetProperty("closed").GetInt32()).ShouldBe((1, 1));
        progress.GetProperty("points_total").GetDecimal().ShouldBe(5m);
        progress.GetProperty("points_done").GetDecimal().ShouldBe(3m);
    }
}
