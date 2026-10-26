using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Backlog;

// review_backlog, sprint_review, move_to_sprint and reorder as Joel's agent on SHIP. Each test arranges its own items with
// words no other test uses, so the shared stack does not disturb the duplicate check.
[TestFixture]
public class BacklogToolsTests : McpFixture
{
    private string _bearer = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task IssueAToken() => _bearer = await TokenAsync(TestUser.Joel, ReadWritePlan);

    [Test]
    public async Task ReviewBacklog_ShouldFlagUnestimatedNoCriteriaStaleAndDuplicates()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Review feature").InsertAsync(connection);
        var rough = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Zebra quokka exporter dark toggle").InsertAsync(connection);
        var twin = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Quokka exporter zebra toggle for dark").WithPoints(2).WithParent(feature).InsertAsync(connection);
        var tidy = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Marmoset ledger reconciliation").WithPoints(3).WithParent(feature).InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET acceptance_criteria = '[{\"text\":\"Done\",\"done\":false}]'::jsonb WHERE id = @tidy", new { tidy });
        await connection.ExecuteAsync("UPDATE work_item SET changed_at = now() - interval '90 days' WHERE id = @rough", new { rough });

        var result = await CallAsync("review_backlog", """{"project":"SHIP","top":100,"stale_days":30}""");

        var items = result.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("key").GetString()!);
        var roughItem = items[await KeyOfAsync(rough)];
        Flags(roughItem).ShouldBe(["unestimated", "no_acceptance_criteria", "no_parent", "stale", "likely_duplicate"], ignoreOrder: true);
        roughItem.GetProperty("duplicates").EnumerateArray().Select(d => d.GetString()).ShouldBe([await KeyOfAsync(twin)]);
        Flags(items[await KeyOfAsync(twin)]).ShouldBe(["no_acceptance_criteria", "likely_duplicate"], ignoreOrder: true);
        Flags(items[await KeyOfAsync(tidy)]).ShouldBeEmpty();
        result.GetProperty("counts").GetProperty("stale").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        result.GetProperty("level").GetString().ShouldBe("stories");
    }

    [Test]
    public async Task SprintReview_ShouldMatchSprintMetrics()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@sprint, @project, 'Review Sprint', DATE '2000-01-01', DATE '2000-01-14')",
            new { sprint, project = SeedIds.Projects.Ship });
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Reviewed feature").InsertAsync(connection);
        var committedOpen = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Committed and open").WithSprint(sprint).WithPoints(3).WithParent(feature).InsertAsync(connection);
        var addedDone = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Added and done").WithSprint(sprint).WithPoints(5).WithParent(feature).WithStateNamed("Closed").InsertAsync(connection);
        var addedOpen = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Added and open").WithSprint(sprint).WithPoints(2).InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = TIMESTAMPTZ '1999-12-30 09:00+00' WHERE work_item_id = @committedOpen", new { committedOpen });
        await connection.ExecuteAsync("UPDATE revision SET changed_at = TIMESTAMPTZ '2000-01-03 09:00+00' WHERE work_item_id = ANY(@added)", new { added = new[] { addedDone, addedOpen } });

        var result = await CallAsync("sprint_review", """{"project":"SHIP","sprint":"Review Sprint"}""");

        using var scope = Host.Services.CreateScope();
        var expected = await scope.ServiceProvider.GetRequiredService<ISprintMetrics>().GetSprintAsync(sprint);
        expected.Committed.ShouldBe(3);
        expected.Completed.ShouldBe(5);
        result.GetProperty("committed").GetDecimal().ShouldBe(expected.Committed);
        result.GetProperty("completed").GetDecimal().ShouldBe(expected.Completed);
        result.GetProperty("remaining").GetDecimal().ShouldBe(expected.Remaining);
        Keys(result.GetProperty("carried_over")).ShouldBe([await KeyOfAsync(committedOpen), await KeyOfAsync(addedOpen)], ignoreOrder: true);
        Keys(result.GetProperty("added_mid_sprint")).ShouldBe([await KeyOfAsync(addedDone), await KeyOfAsync(addedOpen)], ignoreOrder: true);
        var line = result.GetProperty("features").EnumerateArray().Single();
        line.GetProperty("key").GetString().ShouldBe(await KeyOfAsync(feature));
        line.GetProperty("closed").GetInt32().ShouldBe(1);
        line.GetProperty("open").GetInt32().ShouldBe(1);
        result.GetProperty("sections").EnumerateArray().Select(s => s.GetProperty("heading").GetString())
            .ShouldBe(["Completed against committed", "Carried over", "Added mid-sprint", "Velocity", "Feature progress"]);
        result.GetProperty("sections")[0].GetProperty("text").GetString()!.ShouldContain("5 of 3");
    }

    [Test]
    public async Task SprintReview_WhenSprintEnded_ShouldListItemsCarriedToTheNextSprint()
    {
        var nineteen = await CallAsync("sprint_review", """{"project":"SHIP","sprint":"Sprint 19"}""");
        var twentyThree = await CallAsync("sprint_review", """{"project":"SHIP","sprint":"Sprint 23"}""");

        Keys(nineteen.GetProperty("carried_over")).ShouldBe(["SHIP-34"]);
        Keys(twentyThree.GetProperty("carried_over")).ShouldBe(["SHIP-138"]);
        twentyThree.GetProperty("completed").GetDecimal().ShouldBe(21);
    }

    [Test]
    public async Task MoveToSprint_ShouldKeepStackOrder()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@sprint, @project, 'Move Sprint', DATE '2098-01-01', DATE '2098-01-14')",
            new { sprint, project = SeedIds.Projects.Ship });
        var first = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Move first").WithBacklogOrder("8100001").InsertAsync(connection);
        var second = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Move second").WithBacklogOrder("8100002").InsertAsync(connection);
        var third = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Move third").WithBacklogOrder("8100003").InsertAsync(connection);

        var result = await CallAsync("move_to_sprint", $$"""{"project":"SHIP","sprint":"Move Sprint","items_to_move":[{"item":"{{await KeyOfAsync(third)}}"},{"item":"{{await KeyOfAsync(first)}}"}]}""");

        Keys(result.GetProperty("moved")).ShouldBe([await KeyOfAsync(first), await KeyOfAsync(third)]);
        result.GetProperty("sprint").GetString().ShouldBe("Move Sprint");
        (await SprintOfAsync(first)).ShouldBe(sprint);
        (await SprintOfAsync(third)).ShouldBe(sprint);
        (await SprintOfAsync(second)).ShouldBeNull();
        (await OrdersAsync(first, second, third)).ShouldBe(["8100001", "8100002", "8100003"]);

        var back = await CallAsync("move_to_sprint", $$"""{"sprint":"backlog","items_to_move":[{"item":"{{await KeyOfAsync(third)}}"}]}""");

        back.GetProperty("sprint").ValueKind.ShouldBe(JsonValueKind.Null);
        (await SprintOfAsync(third)).ShouldBeNull();
        (await OrdersAsync(first, second, third)).ShouldBe(["8100001", "8100002", "8100003"]);
    }

    [Test]
    public async Task MoveToSprint_WhenNoPlanScope_ShouldAnswer403()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Needs plan scope").InsertAsync(connection);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "move_to_sprint", $$"""{"project":"SHIP","sprint":"next","items_to_move":[{"item":"{{await KeyOfAsync(story)}}"}]}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        reply.WwwAuthenticate.ShouldContain("shiplean:plan");
        (await SprintOfAsync(story)).ShouldBeNull();
    }

    [Test]
    public async Task MoveToSprint_WhenStale_ShouldReportConflict()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@sprint, @project, 'Stale Sprint', DATE '2097-01-01', DATE '2097-01-14')",
            new { sprint, project = SeedIds.Projects.Ship });
        var fresh = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Stale fresh").InsertAsync(connection);
        var stale = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Stale one").InsertAsync(connection);
        // Someone else's change: revision 2.
        await CallAsync("update_progress", $$"""{"item":"{{await KeyOfAsync(stale)}}","points":8}""");

        var reply = await CallToolAsync(_bearer, "move_to_sprint",
            $$"""{"project":"SHIP","sprint":"Stale Sprint","items_to_move":[{"item":"{{await KeyOfAsync(fresh)}}"},{"item":"{{await KeyOfAsync(stale)}}","expected_revision":1}]}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Conflict);
        reply.Structured().GetProperty("error").GetProperty("message").GetString()!.ShouldContain(await KeyOfAsync(stale));
        (await SprintOfAsync(stale)).ShouldBeNull();
        (await SprintOfAsync(fresh)).ShouldBeNull();
    }

    [Test]
    public async Task Reorder_ShouldWriteNoRevision()
    {
        await using var connection = await OpenDatabaseAsync();
        var first = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Reorder first").WithBacklogOrder("8200001").InsertAsync(connection);
        var second = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Reorder second").WithBacklogOrder("8200002").InsertAsync(connection);
        var third = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Reorder third").WithBacklogOrder("8200003").InsertAsync(connection);

        var result = await CallAsync("reorder", $$"""{"item":"{{await KeyOfAsync(third)}}","above":"{{await KeyOfAsync(first)}}"}""");

        result.GetProperty("revision").GetInt32().ShouldBe(1);
        result.GetProperty("position").GetString().ShouldBe($"above {await KeyOfAsync(first)}");
        var orders = await OrdersAsync(third, first, second);
        orders.ShouldBe([.. orders.Order(StringComparer.Ordinal)]);
        using var scope = Host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkItemStore>();
        (await store.GetAsync(third)).Revision.ShouldBe(1);
        (await store.GetRevisionsAsync(third)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Reorder_WhenBelow_ShouldPlaceItDirectlyUnderTheOther()
    {
        await using var connection = await OpenDatabaseAsync();
        var first = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Below first").WithBacklogOrder("8300001").InsertAsync(connection);
        var second = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Below second").WithBacklogOrder("8300002").InsertAsync(connection);
        var third = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Below third").WithBacklogOrder("8300003").InsertAsync(connection);

        await CallAsync("reorder", $$"""{"item":"{{await KeyOfAsync(first)}}","below":"{{await KeyOfAsync(second)}}"}""");
        await CallAsync("reorder", $$"""{"item":"{{await KeyOfAsync(second)}}","below":"{{await KeyOfAsync(third)}}"}""");

        var orders = await OrdersAsync(first, third, second);
        orders.ShouldBe([.. orders.Order(StringComparer.Ordinal)]);
    }

    [Test]
    public async Task Reorder_WhenAboveAndBelowBothGiven_ShouldBeValidation()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Both given").InsertAsync(connection);
        var key = await KeyOfAsync(story);

        var reply = await CallToolAsync(_bearer, "reorder", $$"""{"item":"{{key}}","above":"{{key}}","below":"{{key}}"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation);
    }

    private async Task<JsonElement> CallAsync(string tool, string arguments)
    {
        var reply = await CallToolAsync(_bearer, tool, arguments);
        reply.IsToolError().ShouldBeFalse(reply.Body);
        return reply.Structured();
    }

    private static string? ErrorCode(McpReply reply)
    {
        reply.IsToolError().ShouldBeTrue(reply.Body);
        return reply.Structured().GetProperty("error").GetProperty("code").GetString();
    }

    private static string[] Keys(JsonElement array) => [.. array.EnumerateArray().Select(i => i.GetProperty("key").GetString()!)];

    private static string[] Flags(JsonElement item) => [.. item.GetProperty("flags").EnumerateArray().Select(f => f.GetString()!)];

    private async Task<string> KeyOfAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
    }

    private async Task<Guid?> SprintOfAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @id", new { id });
    }

    // The backlog_order of each item, in the order the ids are given.
    private async Task<string[]> OrdersAsync(params Guid[] ids)
    {
        await using var connection = await OpenDatabaseAsync();
        var orders = new List<string>();
        foreach (var id in ids)
        {
            orders.Add(await connection.QuerySingleAsync<string>("SELECT backlog_order FROM work_item WHERE id = @id", new { id }));
        }

        return [.. orders];
    }
}
