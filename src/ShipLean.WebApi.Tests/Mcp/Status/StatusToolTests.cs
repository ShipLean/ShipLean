using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Status;

// sprint_status, whats_changed_since and feature_progress as Joel on SHIP. One scenario: the arrangement below is made once
// and every test only reads.
[TestFixture]
public class StatusToolTests : McpFixture
{
    private static readonly DateTimeOffset Old = new(2020, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private Guid _alpha;
    private Guid _feature;
    private Guid _changedItem;
    private string _changedKey = string.Empty;
    private string _blockedByTagKey = string.Empty;
    private string _blockedByLinkKey = string.Empty;
    private string _freeKey = string.Empty;
    private string _doneBlockerKey = string.Empty;
    private string _looseTaskKey = string.Empty;
    private string _featureKey = string.Empty;
    private string _otherProjectKey = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeSprintFeatureAndChanges()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = SeedIds.Projects.Ship;
        _alpha = await AddSprintAsync(connection, "Orient Alpha", "2030-01-01", "2030-01-14", "Ship checkout");
        await AddSprintAsync(connection, "Orient Beta", "2030-02-01", "2030-02-14", "");
        var done = await connection.QuerySingleAsync<Guid>("SELECT id FROM state WHERE project_id = @project AND type = 'story' AND category = 'completed' AND archived_at IS NULL LIMIT 1", new { project });

        var tagged = await new WorkItemBuilder(project).WithTitle("Tagged").WithSprint(_alpha).WithTags("blocked").WithAssignee(SeedIds.Users.Priya).WithPoints(3).InsertAsync(connection);
        var waiting = await new WorkItemBuilder(project).WithTitle("Waiting").WithSprint(_alpha).WithPoints(2).InsertAsync(connection);
        var blocker = await new WorkItemBuilder(project).WithTitle("Open blocker").InsertAsync(connection);
        var free = await new WorkItemBuilder(project).WithTitle("Not blocked").WithSprint(_alpha).WithPoints(1).InsertAsync(connection);
        var finished = await new WorkItemBuilder(project).WithTitle("Finished blocker").WithState(done).InsertAsync(connection);
        var loose = await new WorkItemBuilder(project).WithType("task").WithTitle("Loose task").WithSprint(_alpha).InsertAsync(connection);
        await new WorkItemBuilder(project).WithType("task").WithTitle("Parented task").WithSprint(_alpha).WithParent(tagged).InsertAsync(connection);
        await AddLinkAsync(connection, waiting, blocker);
        await AddLinkAsync(connection, free, finished);

        _blockedByTagKey = await KeyAsync(connection, tagged);
        _blockedByLinkKey = await KeyAsync(connection, waiting);
        _freeKey = await KeyAsync(connection, free);
        _doneBlockerKey = await KeyAsync(connection, finished);
        _looseTaskKey = await KeyAsync(connection, loose);

        _feature = await new WorkItemBuilder(project).WithType("feature").WithTitle("Checkout").InsertAsync(connection);
        await new WorkItemBuilder(project).WithTitle("Closed story").WithParent(_feature).WithState(done).WithPoints(5).InsertAsync(connection);
        var open = await new WorkItemBuilder(project).WithTitle("Open story").WithParent(_feature).WithSprint(_alpha).WithPoints(3).InsertAsync(connection);
        await new WorkItemBuilder(project).WithType("task").WithTitle("Open task").WithParent(open).InsertAsync(connection);
        _featureKey = await KeyAsync(connection, _feature);

        var elsewhere = await new WorkItemBuilder(SeedIds.Projects.Part).WithType("feature").WithTitle("Not Joel's").InsertAsync(connection);
        _otherProjectKey = await KeyAsync(connection, elsewhere);

        _changedItem = await new WorkItemBuilder(project).WithTitle("Joel's item").WithAssignee(SeedIds.Users.Joel).InsertAsync(connection);
        _changedKey = await KeyAsync(connection, _changedItem);
        await new RevisionBuilder(_changedItem).WithRev(2).WithChangedAt(Old).WithChangedBy(SeedIds.Users.Priya)
            .WithChanges("""[{"field":"points","from":null,"to":3,"fromLabel":null,"toLabel":null}]""").InsertAsync(connection);
        await new RevisionBuilder(_changedItem).WithRev(3).WithChangedAt(Old.AddDays(2)).WithChangedBy(SeedIds.Users.Priya)
            .WithChanges("""[{"field":"points","from":3,"to":5,"fromLabel":null,"toLabel":null}]""").InsertAsync(connection);
    }

    [Test]
    public async Task SprintStatus_ShouldListBlockedByTagOrLink()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "sprint_status", """{"project":"SHIP","sprint":"Orient Alpha"}""");

        reply.IsToolError().ShouldBeFalse();
        var status = reply.Structured();
        status.GetProperty("project").GetString().ShouldBe("SHIP");
        status.GetProperty("sprint").GetString().ShouldBe("Orient Alpha");
        status.GetProperty("goal").GetString().ShouldBe("Ship checkout");
        status.GetProperty("open_url").GetString().ShouldEndWith($"/p/SHIP/sprints/{_alpha}/taskboard");
        Keys(status.GetProperty("blocked")).ShouldBe([_blockedByTagKey, _blockedByLinkKey], ignoreOrder: true);
        Keys(status.GetProperty("blocked")).ShouldNotContain(_freeKey);
        Keys(status.GetProperty("blocked")).ShouldNotContain(_doneBlockerKey);
        status.GetProperty("blocked").EnumerateArray().Single(i => i.GetProperty("key").GetString() == _blockedByLinkKey)
            .GetProperty("blocked_by").EnumerateArray().Count().ShouldBe(1);
        Keys(status.GetProperty("unparented_tasks")).ShouldBe([_looseTaskKey]);
        var tagged = status.GetProperty("states").EnumerateArray().SelectMany(g => g.GetProperty("items").EnumerateArray())
            .Single(i => i.GetProperty("key").GetString() == _blockedByTagKey);
        tagged.GetProperty("assignee").GetString().ShouldBe("Priya Shah");
        status.GetProperty("days_left").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task SprintStatus_ShouldTakeNumbersFromTheSprintMetrics()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var status = (await CallToolAsync(token, "sprint_status", """{"project":"SHIP","sprint":"Orient Alpha"}""")).Structured();

        using var scope = Host.Services.CreateScope();
        var expected = await scope.ServiceProvider.GetRequiredService<ShipLean.Contracts.Sprints.ISprintMetrics>().GetSprintAsync(_alpha);
        status.GetProperty("committed").GetDecimal().ShouldBe(expected.Committed);
        status.GetProperty("completed").GetDecimal().ShouldBe(expected.Completed);
        status.GetProperty("remaining").GetDecimal().ShouldBe(expected.Remaining);
        status.GetProperty("burndown").GetArrayLength().ShouldBe(expected.Burndown.Count);
    }

    [Test]
    public async Task SprintStatus_WhenSprintNameAmbiguous_ShouldListCandidates()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "sprint_status", """{"project":"SHIP","sprint":"Orient"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Ambiguous);
        error.GetProperty("candidates").EnumerateArray().Select(c => c.GetString()).ShouldBe(["Orient Alpha", "Orient Beta"]);
    }

    [Test]
    public async Task ChangesSince_ShouldReturnCursor()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var first = (await CallToolAsync(token, "whats_changed_since", """{"project":"SHIP","since":"2019-12-31T00:00:00Z"}""")).Structured();
        var cursor = first.GetProperty("cursor").GetString();
        var second = (await CallToolAsync(token, "whats_changed_since", $$"""{"project":"SHIP","cursor":"{{cursor}}"}""")).Structured();

        cursor.ShouldNotBeNullOrEmpty();
        first.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()).ShouldContain(_changedKey);
        first.GetProperty("more").GetBoolean().ShouldBeFalse();
        second.GetProperty("items").GetArrayLength().ShouldBe(0);
        second.GetProperty("cursor").GetString().ShouldBe(cursor);
    }

    [Test]
    public async Task ChangesSince_WhenSinceTime_ShouldStartThere()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var changes = (await CallToolAsync(token, "whats_changed_since", """{"project":"SHIP","since":"2020-01-02T00:00:00Z"}""")).Structured();

        var item = changes.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == _changedKey);
        var revisions = item.GetProperty("changes").EnumerateArray().Select(c => c.GetProperty("revision").GetInt32()).ToList();
        revisions.ShouldContain(3);
        revisions.ShouldNotContain(2);
        var third = item.GetProperty("changes").EnumerateArray().Single(c => c.GetProperty("revision").GetInt32() == 3);
        third.GetProperty("by").GetString().ShouldBe("Priya Shah");
        third.GetProperty("fields")[0].GetString().ShouldBe("points: 3 -> 5");
    }

    [Test]
    public async Task ChangesSince_WhenMineIsFalse_ShouldIncludeOthersItems()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var mine = (await CallToolAsync(token, "whats_changed_since", """{"project":"SHIP","since":"2020-01-02T00:00:00Z"}""")).Structured();
        var all = (await CallToolAsync(token, "whats_changed_since", """{"project":"SHIP","since":"2020-01-02T00:00:00Z","mine":false,"limit":500}""")).Structured();

        Keys(mine.GetProperty("items")).ShouldNotContain(_blockedByTagKey);
        Keys(all.GetProperty("items")).ShouldContain(_blockedByTagKey);
    }

    [Test]
    public async Task ChangesSince_WhenNeitherCursorNorTime_ShouldBeValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "whats_changed_since", """{"project":"SHIP"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase("yesterday")]
    [TestCase("01/02/2020")]
    public async Task ChangesSince_WhenTimeIsNotIso8601_ShouldBeValidation(string since)
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "whats_changed_since", $$"""{"project":"SHIP","since":"{{since}}"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task ChangesSince_WhenProjectJoelIsNotInAndNoTime_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "whats_changed_since", """{"project":"PART"}""");

        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task FeatureProgress_ShouldMatchWorkTreeRollup()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var progress = (await CallToolAsync(token, "feature_progress", $$"""{"item":"{{_featureKey}}"}""")).Structured();

        using var scope = Host.Services.CreateScope();
        var rollups = await scope.ServiceProvider.GetRequiredService<IWorkTreeReads>().RollupsAsync(SeedIds.Projects.Ship, [_feature], null);
        var byType = progress.GetProperty("by_type").EnumerateArray().ToList();
        byType.Select(t => (t.GetProperty("type").GetString()!, t.GetProperty("open").GetInt32(), t.GetProperty("closed").GetInt32(), Points(t.GetProperty("points_total"))))
            .ShouldBe(rollups.Select(r => (r.Type.ToString().ToLowerInvariant(), r.Open, r.Closed, r.TotalPoints)), ignoreOrder: true);
        progress.GetProperty("item").GetString().ShouldBe(_featureKey);
        progress.GetProperty("points_total").GetDecimal().ShouldBe(8m);
        progress.GetProperty("points_done").GetDecimal().ShouldBe(5m);
        var open = progress.GetProperty("open_children").EnumerateArray().ToList();
        open.Select(c => c.GetProperty("title").GetString()).ShouldBe(["Open story", "Open task"], ignoreOrder: true);
        open.Single(c => c.GetProperty("title").GetString() == "Open story").GetProperty("sprint").GetString().ShouldBe("Orient Alpha");
        progress.GetProperty("open_total").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task FeatureProgress_WhenItemInProjectJoelIsNotIn_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "feature_progress", $$"""{"item":"{{_otherProjectKey}}"}""");

        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    private static decimal? Points(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetDecimal();

    private static List<string?> Keys(JsonElement items) => [.. items.EnumerateArray().Select(i => i.GetProperty("key").GetString())];

    private static Task<string> KeyAsync(Npgsql.NpgsqlConnection connection, Guid id)
        => connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });

    private static async Task<Guid> AddSprintAsync(Npgsql.NpgsqlConnection connection, string name, string start, string end, string goal)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date, goal) VALUES (@id, @project, @name, @start::date, @end::date, @goal)",
            new { id, project = SeedIds.Projects.Ship, name, start, end, goal });
        return id;
    }

    private static Task AddLinkAsync(Npgsql.NpgsqlConnection connection, Guid source, Guid target)
        => connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (@id, @source, @target, 'blockedBy')",
            new { id = Guid.NewGuid(), source, target });
}
