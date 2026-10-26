using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;
using ShipLean.WebApi.Tests.Mcp.Work;

namespace ShipLean.WebApi.Tests.Mcp.Capture;

// capture_work and break_down against the seed, as Joel (a member of SHIP, PAY, MOB, DATA, OPS and EMPT, not PART).
[TestFixture]
public class CaptureToolsTests : McpFixture
{
    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddSingleton<StoreCreates>();
        WorkItemStoreDecoration.Decorate(services, (real, sp) => new RecordingWorkItemStore(real, sp.GetRequiredService<StoreCreates>()));
    };

    private StoreCreates Creates => Host.Services.GetRequiredService<StoreCreates>();

    private const string Two = """[{"type":"story","title":"Capture one","points":3,"sprint":"next","tags":["agent"]},{"type":"bug","title":"Capture two","severity":2,"repro_steps":"Open it"}]""";

    private async Task<int> CountAsync(string title)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item WHERE title = @title", new { title });
    }

    [Test]
    public async Task Capture_WhenManyItems_ShouldPreviewThenCreateInOneTransaction()
    {
        var readOnly = await TokenAsync(TestUser.Joel, ReadOnly);
        var readWrite = await TokenAsync(TestUser.Joel, ReadWrite);

        var preview = await CallToolAsync(readOnly, "capture_work", $$"""{"project":"SHIP","items":{{Two}}}""");

        preview.IsToolError().ShouldBeFalse();
        var previewed = preview.Structured();
        previewed.GetProperty("applied").GetBoolean().ShouldBeFalse();
        previewed.GetProperty("project").GetString().ShouldBe("SHIP");
        previewed.GetProperty("items").GetArrayLength().ShouldBe(2);
        previewed.GetProperty("items")[0].GetProperty("sprint").GetString().ShouldBe("Sprint 25");
        (await CountAsync("Capture one")).ShouldBe(0);
        var plan = previewed.GetProperty("plan").GetString()!;

        var refused = await CallToolAsync(readOnly, "capture_work", $$"""{"project":"SHIP","plan":"{{plan}}"}""");
        refused.Status.ShouldBe(HttpStatusCode.Forbidden);

        // The plan holds the items: a different list passed with it changes nothing.
        Creates.Reset();
        var applied = await CallToolAsync(readWrite, "capture_work", $$"""{"project":"SHIP","plan":"{{plan}}","items":[{"type":"task","title":"Not in the plan"}]}""");

        applied.IsToolError().ShouldBeFalse();
        var created = applied.Structured().GetProperty("items").EnumerateArray().ToList();
        created.Select(i => i.GetProperty("title").GetString()).ShouldBe(["Capture one", "Capture two"]);
        var numbers = created.Select(i => int.Parse(i.GetProperty("key").GetString()![5..])).ToList();
        numbers[1].ShouldBe(numbers[0] + 1);
        Creates.CreateManySizes.ShouldBe([2]);
        Creates.Creates.ShouldBe(0);
        (await CountAsync("Capture one")).ShouldBe(1);
        (await CountAsync("Not in the plan")).ShouldBe(0);
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(decimal? Points, Guid? SprintId, string Type)>(
            "SELECT story_points, sprint_id, type FROM work_item WHERE key = @key", new { key = created[0].GetProperty("key").GetString() });
        row.ShouldBe((3m, SeedIds.Sprints.Sprint25, "story"));
    }

    [Test]
    public async Task Capture_WhenOneItem_ShouldCreateStraightAway()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"task","title":"Single capture"}]}""");

        reply.Structured().GetProperty("items").GetArrayLength().ShouldBe(1);
        (await CountAsync("Single capture")).ShouldBe(1);
    }

    [Test]
    public async Task Capture_WhenOneItemAndReadOnlyToken_ShouldAnswer403AndCreateNothing()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"task","title":"Read only capture"}]}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        reply.WwwAuthenticate.ShouldContain("insufficient_scope");
        (await CountAsync("Read only capture")).ShouldBe(0);
    }

    [Test]
    public async Task Capture_WhenNoProjectAndMany_ShouldBeValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_work", $$"""{"items":{{Two}}}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("project");
    }

    [Test]
    public async Task Capture_WhenTwentySix_ShouldBeValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var items = string.Join(",", Enumerable.Range(1, 26).Select(i => $$"""{"type":"task","title":"Too many {{i}}"}"""));

        var reply = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","items":[{{items}}]}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("items");
        (await CountAsync("Too many 1")).ShouldBe(0);
    }

    [Test]
    public async Task Capture_WhenPlanTampered_ShouldBeValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var plan = (await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","items":[{"type":"story","title":"Tamper one"},{"type":"bug","title":"Tamper two"}]}""")).Structured().GetProperty("plan").GetString()!;
        var tampered = plan[..^4] + (plan.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        var reply = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","plan":"{{tampered}}"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("plan");
        (await CountAsync("Tamper one")).ShouldBe(0);
    }

    [Test]
    public async Task BreakDown_ShouldInheritParentSprint()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        Guid parent;
        string parentKey;
        Guid parentState;
        await using (var connection = await OpenDatabaseAsync())
        {
            parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Break me down").WithSprint(SeedIds.Sprints.Sprint24).InsertAsync(connection);
            parentKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @parent", new { parent });
            parentState = await connection.QuerySingleAsync<Guid>("SELECT state_id FROM work_item WHERE id = @parent", new { parent });
        }

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{parentKey}}","children":[{"title":"Break one","points":1},{"title":"Break two"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var keys = reply.Structured().GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()).ToList();
        keys.Count.ShouldBe(2);
        await using var check = await OpenDatabaseAsync();
        var children = (await check.QueryAsync<(Guid? Sprint, Guid? Parent, string Type)>(
            "SELECT sprint_id, parent_id, type FROM work_item WHERE key = ANY(@keys)", new { keys = keys.ToArray() })).ToList();
        children.ShouldAllBe(c => c.Sprint == SeedIds.Sprints.Sprint24 && c.Parent == parent && c.Type == "task");
        (await check.QuerySingleAsync<Guid>("SELECT state_id FROM work_item WHERE id = @parent", new { parent })).ShouldBe(parentState);
    }

    [Test]
    public async Task BreakDown_WhenParentIsBug_ShouldCreateTasks()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        string parentKey;
        await using (var connection = await OpenDatabaseAsync())
        {
            var parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bug").WithTitle("Break a bug down").InsertAsync(connection);
            parentKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @parent", new { parent });
        }

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{parentKey}}","children":[{"title":"Bug task one"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("items")[0].GetProperty("type").GetString().ShouldBe("task");
    }

    [Test]
    public async Task Capture_WhenPlanSprintArchivedBeforeApply_ShouldBeConflictAndCreateNothing()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, 'Recheck Sprint', DATE '2090-01-01', DATE '2090-01-14')",
                new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship });
        }
        var preview = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"story","title":"Recheck one","sprint":"Recheck Sprint"},{"type":"story","title":"Recheck two"}]}""");
        var plan = preview.Structured().GetProperty("plan").GetString()!;
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE sprint SET archived_at = now() WHERE name = 'Recheck Sprint' AND project_id = @project", new { project = SeedIds.Projects.Ship });
        }

        var reply = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","plan":"{{plan}}"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Conflict);
        reply.ResultText().ShouldContain("preview again");
        (await CountAsync("Recheck one")).ShouldBe(0);
        (await CountAsync("Recheck two")).ShouldBe(0);
    }

    [Test]
    public async Task Capture_WhenPlanParentRemovedBeforeApply_ShouldBeConflictAndCreateNothing()
    {
        var (token, plan, parent) = await PreviewUnderNewParentAsync("Removed parent", "Parent gone");
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = @project AND type = 'feature' AND category = 'removed' LIMIT 1) WHERE id = @parent",
                new { project = SeedIds.Projects.Ship, parent });
        }

        var reply = await ApplyPlanAsync(token, plan);

        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Conflict);
        reply.ResultText().ShouldContain("preview again");
        (await CountAsync("Parent gone one")).ShouldBe(0);
        (await CountAsync("Parent gone two")).ShouldBe(0);
    }

    [Test]
    public async Task Capture_WhenPlanParentMovedToAnotherProjectBeforeApply_ShouldBeConflictAndCreateNothing()
    {
        var (token, plan, parent) = await PreviewUnderNewParentAsync("Moved parent", "Parent moved");
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE work_item SET project_id = @project, state_id = (SELECT id FROM state WHERE project_id = @project AND type = 'feature' AND category = 'proposed' AND archived_at IS NULL LIMIT 1) WHERE id = @parent",
                new { project = SeedIds.Projects.Pay, parent });
        }

        var reply = await ApplyPlanAsync(token, plan);

        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Conflict);
        reply.ResultText().ShouldContain("preview again");
        (await CountAsync("Parent moved one")).ShouldBe(0);
        (await CountAsync("Parent moved two")).ShouldBe(0);
    }

    [Test]
    public async Task Capture_WhenPlanSprintRenamedBeforeApply_ShouldStillApply()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, 'Before Rename', DATE '2091-01-01', DATE '2091-01-14')",
                new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship });
        }
        var preview = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"story","title":"Rename one","sprint":"Before Rename"},{"type":"story","title":"Rename two"}]}""");
        var plan = preview.Structured().GetProperty("plan").GetString()!;
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE sprint SET name = 'After Rename' WHERE name = 'Before Rename' AND project_id = @project", new { project = SeedIds.Projects.Ship });
        }

        var reply = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","plan":"{{plan}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        (await CountAsync("Rename one")).ShouldBe(1);
    }

    private async Task<(string Token, string Plan, Guid Parent)> PreviewUnderNewParentAsync(string parentTitle, string prefix)
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        string parentKey;
        Guid parent;
        await using (var connection = await OpenDatabaseAsync())
        {
            parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle(parentTitle).InsertAsync(connection);
            parentKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @parent", new { parent });
        }
        var preview = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","items":[{"type":"story","title":"{{prefix}} one","parent":"{{parentKey}}"},{"type":"story","title":"{{prefix}} two","parent":"{{parentKey}}"}]}""");
        preview.IsToolError().ShouldBeFalse(preview.ResultText());
        return (token, preview.Structured().GetProperty("plan").GetString()!, parent);
    }

    private async Task<McpReply> ApplyPlanAsync(string token, string plan)
        => await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","plan":"{{plan}}"}""");
}
