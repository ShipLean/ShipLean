using System.Reflection;
using System.Text.Json;
using Dapper;
using ModelContextProtocol.Server;
using Npgsql;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Retros;

// decide_retro_actions (K8): the scope rule, the three-new-actions limit, what a preview lists, and that an apply does what the plan holds.
// Each scenario arranges its own sprint (or Feature, or far-past days) so the points of one never reach another.
[TestFixture]
public class DecideRetroActionsTests : McpFixture
{
    private const string Feature = "SHIP-9";

    protected override bool WithProbeTools => false;

    [Test]
    public void Tool_ShouldBeRegisteredWithPlanScope()
    {
        var tool = typeof(ApiMarker).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Single(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name == "decide_retro_actions");

        var scope = tool.GetCustomAttribute<McpScopeAttribute>()!;

        scope.Scope.ShouldBe(McpScopes.Plan);
        scope.PreviewScope.ShouldBe(McpScopes.Read);
    }

    [Test]
    public async Task Preview_ShouldReturnAPlanAndTheContractsFields()
    {
        var world = await WorldAsync();
        var point = await world.PointAsync(RetroCategory.WentBadly, "Point");

        var result = await world.PreviewAsync($$""", "actions":[{{New("Fix " + world.Tag, point)}}]""");

        result.GetProperty("plan").GetString().ShouldNotBeNullOrEmpty();
        result.GetProperty("expires_in_minutes").GetInt32().ShouldBe(15);
        Names(result).ShouldBe(
            ["applied", "created", "expires_in_minutes", "left_undecided", "open_url", "plan", "project", "skipped", "would_add", "would_create", "would_mark_not_needed", "would_propose_merges", "would_theme"], ignoreOrder: true);
    }

    [Test]
    public async Task Apply_WhenPlanFromAnotherUser_ShouldBeRefused()
    {
        var world = await WorldAsync();
        var plan = await world.PlanAsync($$""", "actions":[{{New("Fix " + world.Tag, await world.PointAsync(RetroCategory.WentBadly, "Point"))}}]""");
        var mei = await TokenAsync(TestUser.Mei, ReadWritePlan);

        var reply = await CallToolAsync(mei, "decide_retro_actions", $$"""{"project":"SHIP","plan":"{{plan}}"}""");

        ErrorField(reply).ShouldBe(("validation", "plan"));
    }

    [Test]
    public async Task Preview_WhenNoScope_ShouldBeValidation()
    {
        var reply = await PreviewRawAsync("""{"project":"SHIP"}""");

        ErrorField(reply).ShouldBe(("validation", "scope"));
    }

    [Test]
    public async Task Preview_WhenFeatureAndSprint_ShouldBeValidation()
    {
        var reply = await PreviewRawAsync($$"""{"project":"SHIP","feature":"{{Feature}}","sprint":"current"}""");

        ErrorField(reply).ShouldBe(("validation", "scope"));
    }

    [TestCase("""{"project":"SHIP","from":"2026-10-01"}""")]
    [TestCase("""{"project":"SHIP","to":"2026-10-01"}""")]
    public async Task Preview_WhenFromWithoutTo_ShouldBeValidation(string arguments)
    {
        var reply = await PreviewRawAsync(arguments);

        ErrorField(reply).ShouldBe(("validation", "scope"));
    }

    [Test]
    public async Task Preview_ShouldChangeNothing()
    {
        var world = await WorldAsync();
        var first = await world.PointAsync(RetroCategory.WentBadly, "First");
        var second = await world.PointAsync(RetroCategory.Improve, "Second");
        var third = await world.PointAsync(RetroCategory.WentBadly, "Third");
        await world.Seed.ThemeAsync($"Left {world.Tag}");
        await world.Seed.ThemeAsync($"Right {world.Tag}");
        var before = await SnapshotAsync();

        var result = await world.PreviewAsync($$"""
            "actions":[{"title":"Fix {{world.Tag}}","fix_type":"skill","points":["{{first}}"]}],
            "not_needed":[{"point":"{{second}}","reason":"Not a problem"}],
            "themes":[{"point":"{{third}}","theme":"Brand new {{world.Tag}}"}],
            "merges":[{"from":"Left {{world.Tag}}","into":"Right {{world.Tag}}","reason":"Same thing"}]
            """);

        result.GetProperty("applied").GetBoolean().ShouldBeFalse();
        result.GetProperty("would_create").GetArrayLength().ShouldBe(1);
        result.GetProperty("would_mark_not_needed").GetArrayLength().ShouldBe(1);
        result.GetProperty("would_theme").GetInt32().ShouldBe(1);
        result.GetProperty("would_propose_merges").GetInt32().ShouldBe(1);
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Preview_WhenFeature_ShouldIncludeItsStoriesPoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").InsertAsync(connection);
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithParent(feature).InsertAsync(connection);
        var bug = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bug").WithParent(feature).InsertAsync(connection);
        var stranger = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").InsertAsync(connection);
        var actionChild = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithParent(feature).InsertAsync(connection);
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        await seed.PointAsync(await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: feature, AgentName: Agent())), new RetroSeedPoint(RetroCategory.WentBadly, "Own"));
        var inActionChild = await seed.PointAsync(await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: actionChild, AgentName: Agent())), new RetroSeedPoint(RetroCategory.WentBadly, "In action child"));
        var inStory = await seed.PointAsync(await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: story, AgentName: Agent())), new RetroSeedPoint(RetroCategory.WentBadly, "In story"));
        var inBug = await seed.PointAsync(await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: bug, AgentName: Agent())), new RetroSeedPoint(RetroCategory.Improve, "In bug"));
        var outside = await seed.PointAsync(await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: stranger, AgentName: Agent())), new RetroSeedPoint(RetroCategory.WentBadly, "Outside"));
        var key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var inside = await CallToolAsync(token, "decide_retro_actions", $$"""{"project":"SHIP","feature":"{{key}}","not_needed":[{"point":"{{inStory}}","reason":"r"},{"point":"{{inBug}}","reason":"r"}]}""");
        var refused = await CallToolAsync(token, "decide_retro_actions", $$"""{"project":"SHIP","feature":"{{key}}","not_needed":[{"point":"{{outside}}","reason":"r"}]}""");

        inside.IsToolError().ShouldBeFalse(inside.Body);
        inside.Structured().GetProperty("would_mark_not_needed").GetArrayLength().ShouldBe(2);
        inside.Structured().GetProperty("left_undecided").GetInt32().ShouldBe(1);
        ErrorField(refused).Code.ShouldBe("validation");
        var actionRefused = await CallToolAsync(token, "decide_retro_actions", $$"""{"project":"SHIP","feature":"{{key}}","not_needed":[{"point":"{{inActionChild}}","reason":"r"}]}""");
        ErrorField(actionRefused).Code.ShouldBe("validation");
    }

    [Test]
    public async Task Preview_WhenSprint_ShouldOnlyCoverThatSprintsPoints()
    {
        var world = await WorldAsync();
        var elsewhere = await WorldAsync();
        await world.PointAsync(RetroCategory.WentBadly, "Here one");
        await world.PointAsync(RetroCategory.Improve, "Here two");
        var there = await elsewhere.PointAsync(RetroCategory.WentBadly, "There");

        var result = await world.PreviewAsync("");
        var refused = await world.PreviewRawAsync($$""", "not_needed":[{"point":"{{there}}","reason":"r"}]""");

        result.GetProperty("left_undecided").GetInt32().ShouldBe(2);
        ErrorField(refused).Code.ShouldBe("validation");
    }

    [Test]
    public async Task Preview_WhenDateRange_ShouldIncludeBothEndDaysInUtc()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var sprint = await SprintAsync(connection);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, SprintId: sprint.Id, AgentName: Agent()));
        var utc = TimeSpan.Zero;
        var edges = new[]
        {
            new DateTimeOffset(1993, 3, 9, 23, 59, 59, utc),
            new DateTimeOffset(1993, 3, 10, 0, 0, 0, utc),
            new DateTimeOffset(1993, 3, 11, 12, 0, 0, utc),
            new DateTimeOffset(1993, 3, 12, 23, 59, 59, utc),
            new DateTimeOffset(1993, 3, 13, 0, 0, 0, utc),
        };
        foreach (var at in edges)
        {
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"At {at:O}", CreatedAt: at));
        }

        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var reply = await CallToolAsync(token, "decide_retro_actions", """{"project":"SHIP","from":"1993-03-10","to":"1993-03-12"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("left_undecided").GetInt32().ShouldBe(3);
    }

    [Test]
    public async Task Preview_WhenFourNew_ShouldBeRefusedNamingTheLimit()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(4);

        var reply = await world.PreviewRawAsync(world.NewActions(points));

        ErrorField(reply).ShouldBe(("validation", "actions"));
        reply.Body.ShouldContain("3 new action items");
    }

    [Test]
    public async Task Preview_WhenThreeNewAndFiveExisting_ShouldPass()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(8);
        var existing = await world.ExistingActionAsync($"Open {world.Tag}", "New");
        var actions = string.Join(",", points.Take(3).Select((id, index) => New($"New {index} {world.Tag}", id)).Concat(points.Skip(3).Select(id => $$"""{"title":"Add","fix_type":"skill","points":["{{id}}"],"existing":"{{existing.Key}}"}""")));

        var result = await world.PreviewAsync($$""", "actions":[{{actions}}]""");

        result.GetProperty("would_create").GetArrayLength().ShouldBe(3);
        result.GetProperty("would_add").GetArrayLength().ShouldBe(5);
        result.GetProperty("would_add")[0].GetProperty("existing").GetString().ShouldBe(existing.Key);
        result.GetProperty("left_undecided").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task Preview_WhenFiveNotNeeded_ShouldPass()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(5);

        var result = await world.PreviewAsync($$""", "not_needed":[{{NotNeededEntries(points)}}]""");

        result.GetProperty("would_mark_not_needed").GetArrayLength().ShouldBe(5);
        result.GetProperty("would_mark_not_needed")[0].GetProperty("reason").GetString().ShouldBe("Not a problem");
    }

    [Test]
    public async Task Preview_WhenSimilarOpenTitle_ShouldReportPossibleDuplicate()
    {
        var world = await WorldAsync();
        var point = await world.PointAsync(RetroCategory.WentBadly, "Flaky");
        var open = await world.ExistingActionAsync($"Stabilise flaky browser tests {world.Tag}", "Active");
        await world.ExistingActionAsync($"Stabilise flaky browser tests {world.Tag} again", "Closed");

        var result = await world.PreviewAsync($$""", "actions":[{"title":"Flaky browser tests {{world.Tag}}: stabilise","fix_type":"test","points":["{{point}}"]}]""");

        var duplicates = result.GetProperty("would_create")[0].GetProperty("possible_duplicates");
        duplicates.GetArrayLength().ShouldBe(1);
        duplicates[0].GetProperty("key").GetString().ShouldBe(open.Key);
        duplicates[0].GetProperty("title").GetString().ShouldBe(open.Title);
        duplicates[0].GetProperty("state").GetString().ShouldBe("Active");
    }

    [Test]
    public async Task Preview_WhenPointDecided_ShouldSkipIt()
    {
        var world = await WorldAsync();
        var free = await world.PointAsync(RetroCategory.WentBadly, "Free");
        var closed = await world.PointAsync(RetroCategory.WentBadly, "Closed", notNeededReason: "Fine");
        var taken = await world.PointAsync(RetroCategory.WentBadly, "Taken");
        await world.ExistingActionAsync($"Holder {world.Tag}", "New", taken);

        var result = await world.PreviewAsync($$""", "actions":[{"title":"Fix {{world.Tag}}","fix_type":"skill","points":["{{free}}","{{closed}}","{{taken}}"]}]""");

        result.GetProperty("would_create")[0].GetProperty("covers").GetInt32().ShouldBe(1);
        Skipped(result).ShouldBe([closed, taken], ignoreOrder: true);
        result.GetProperty("left_undecided").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task Preview_WhenWentWell_ShouldBeValidation()
    {
        var world = await WorldAsync();
        var well = await world.PointAsync(RetroCategory.WentWell, "Good");

        var reply = await world.PreviewRawAsync(world.NewActions([well]));

        ErrorField(reply).ShouldBe(("validation", "actions[0].points"));
    }

    [Test]
    public async Task Preview_WhenPointOutsideScope_ShouldBeValidation()
    {
        var world = await WorldAsync();
        var elsewhere = await WorldAsync();
        var outside = await elsewhere.PointAsync(RetroCategory.WentBadly, "Outside");

        var reply = await world.PreviewRawAsync(world.NewActions([outside]));

        ErrorField(reply).ShouldBe(("validation", "actions[0].points"));
    }

    [Test]
    public async Task Preview_WhenPointInTwoEntries_ShouldBeValidation()
    {
        var world = await WorldAsync();
        var point = await world.PointAsync(RetroCategory.WentBadly, "Twice");

        var reply = await world.PreviewRawAsync($$""", "actions":[{{New("One " + world.Tag, point)}}], "not_needed":[{"point":"{{point}}","reason":"r"}]""");

        ErrorField(reply).ShouldBe(("validation", "not_needed[0].point"));
    }

    [Test]
    public async Task Apply_ShouldCreateInNewWithNoSprint()
    {
        var world = await WorldAsync();
        var first = await world.PointAsync(RetroCategory.WentBadly, "First");
        var second = await world.PointAsync(RetroCategory.Improve, "Second");

        var applied = await world.ApplyAsync(await world.PlanAsync($$""", "actions":[{"title":"Fix {{world.Tag}}","fix_type":"analyzer","points":["{{first}}","{{second}}"]}]"""));

        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();
        var key = applied.GetProperty("created").EnumerateArray().Single().GetString()!;
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string State, Guid? Sprint, Guid? Parent, string FixType)>(
            "SELECT s.name AS State, w.sprint_id AS Sprint, w.parent_id AS Parent, ra.fix_type AS FixType FROM work_item w JOIN state s ON s.id = w.state_id JOIN retro_action ra ON ra.work_item_id = w.id WHERE w.key = @key", new { key });
        row.ShouldBe(("New", null, null, "analyzer"));
        (await connection.QueryAsync<Guid>("SELECT point_id FROM retro_action_point rap JOIN work_item w ON w.id = rap.work_item_id WHERE w.key = @key AND rap.archived_at IS NULL ORDER BY position", new { key })).ShouldBe([first, second]);
    }

    [Test]
    public async Task Apply_WhenPointDecidedSincePreview_ShouldSkipIt()
    {
        var world = await WorldAsync();
        var kept = await world.PointAsync(RetroCategory.WentBadly, "Kept");
        var late = await world.PointAsync(RetroCategory.WentBadly, "Decided late");
        var alone = await world.PointAsync(RetroCategory.WentBadly, "Alone");
        var plan = await world.PlanAsync($$""", "actions":[{{New("One " + world.Tag, kept, late)}},{{New("Two " + world.Tag, alone)}}]""");
        await world.DecideAsync(late);
        await world.DecideAsync(alone);

        var applied = await world.ApplyAsync(plan);

        applied.GetProperty("created").GetArrayLength().ShouldBe(1);
        Skipped(applied).ShouldBe([late, alone], ignoreOrder: true);
        await using var connection = await OpenDatabaseAsync();
        var key = applied.GetProperty("created")[0].GetString();
        (await connection.QueryAsync<Guid>("SELECT point_id FROM retro_action_point rap JOIN work_item w ON w.id = rap.work_item_id WHERE w.key = @key", new { key })).ShouldBe([kept]);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE title = @title", new { title = "Two " + world.Tag })).ShouldBe(0);
    }

    [Test]
    public async Task Preview_WhenFixTypeIsANumber_ShouldBeValidationNamingTheField()
    {
        var world = await WorldAsync();
        var point = await world.PointAsync(RetroCategory.WentBadly, "Point");

        var reply = await world.PreviewRawAsync($$""", "actions":[{"title":"Numeric {{world.Tag}}","fix_type":"999","points":["{{point}}"]}]""");

        ErrorField(reply).ShouldBe(("validation", "actions[0].fix_type"));
    }

    private const string RaceTriggerSql =
        """
        CREATE OR REPLACE FUNCTION decide_race() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
          UPDATE retro_point SET not_needed_reason = 'Late.', decided_by_user_id = created_by_user_id, decided_at = now()
          WHERE not_needed_reason IS NULL
            AND text LIKE 'late' || (SELECT count(*) FROM work_item WHERE title = NEW.title AND project_id = NEW.project_id)::text || ':' || NEW.title || ' %';
          RETURN NEW; END $$;
        DROP TRIGGER IF EXISTS decide_race ON work_item;
        CREATE TRIGGER decide_race AFTER INSERT ON work_item FOR EACH ROW WHEN (NEW.title LIKE 'Race %') EXECUTE FUNCTION decide_race();
        """;

    private const string RaceTriggerDropSql = "DROP TRIGGER IF EXISTS decide_race ON work_item; DROP FUNCTION IF EXISTS decide_race();";

    // Stands in for another request: the Nth time an action titled "Race ..." is inserted, the point whose text is "lateN:<title> <tag>" gets decided.
    private async Task WithRaceAsync(Func<Task> act)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(RaceTriggerSql);
        try
        {
            await act();
        }
        finally
        {
            await connection.ExecuteAsync(RaceTriggerDropSql);
        }
    }

    [Test]
    public async Task Apply_WhenOnePointIsDecidedBetweenReadAndWrite_ShouldRetryWithTheRest()
    {
        var world = await WorldAsync();
        var title = "Race " + world.Tag;
        var kept = await world.PointAsync(RetroCategory.WentBadly, "Kept");
        var late = await world.PointAsync(RetroCategory.WentBadly, "late1:" + title);
        var plan = await world.PlanAsync($$""", "actions":[{{New(title, kept, late)}}]""");
        JsonElement applied = default;

        await WithRaceAsync(async () => applied = await world.ApplyAsync(plan));

        applied.GetProperty("created").GetArrayLength().ShouldBe(1);
        Skipped(applied).ShouldBe([late]);
        await using var connection = await OpenDatabaseAsync();
        var key = applied.GetProperty("created")[0].GetString();
        (await connection.QueryAsync<Guid>("SELECT point_id FROM retro_action_point rap JOIN work_item w ON w.id = rap.work_item_id WHERE w.key = @key AND rap.archived_at IS NULL", new { key })).ShouldBe([kept]);
    }

    [Test]
    public async Task Apply_WhenPointsAreDecidedInSuccessivePasses_ShouldKeepRetryingWithTheRest()
    {
        var world = await WorldAsync();
        var title = "Race " + world.Tag;
        var kept = await world.PointAsync(RetroCategory.WentBadly, "Kept");
        var first = await world.PointAsync(RetroCategory.WentBadly, "late1:" + title);
        var second = await world.PointAsync(RetroCategory.WentBadly, "late2:" + title);
        var plan = await world.PlanAsync($$""", "actions":[{{New(title, kept, first, second)}}]""");
        JsonElement applied = default;

        await WithRaceAsync(async () => applied = await world.ApplyAsync(plan));

        applied.GetProperty("created").GetArrayLength().ShouldBe(1);
        Skipped(applied).ShouldBe([first, second], ignoreOrder: true);
        await using var connection = await OpenDatabaseAsync();
        var key = applied.GetProperty("created")[0].GetString();
        (await connection.QueryAsync<Guid>("SELECT point_id FROM retro_action_point rap JOIN work_item w ON w.id = rap.work_item_id WHERE w.key = @key AND rap.archived_at IS NULL", new { key })).ShouldBe([kept]);
    }

    // The same race when the decided point is the action's only one: nothing is created.
    [Test]
    public async Task Apply_WhenTheOnlyPointIsDecidedBetweenReadAndWrite_ShouldCreateNothing()
    {
        var world = await WorldAsync();
        var title = "Race " + world.Tag;
        var late = await world.PointAsync(RetroCategory.WentBadly, "late1:" + title);
        var plan = await world.PlanAsync($$""", "actions":[{{New(title, late)}}]""");
        JsonElement applied = default;

        await WithRaceAsync(async () => applied = await world.ApplyAsync(plan));

        applied.GetProperty("created").GetArrayLength().ShouldBe(0);
        Skipped(applied).ShouldBe([late]);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM work_item w JOIN state st ON st.id = w.state_id WHERE w.title = @title AND st.category <> 'removed'", new { title })).ShouldBe(0);
    }

    [Test]
    public async Task Apply_ShouldApplyThePlanNotTheArguments()
    {
        var world = await WorldAsync();
        var planned = await world.PointAsync(RetroCategory.WentBadly, "Planned");
        var other = await world.PointAsync(RetroCategory.WentBadly, "Other");
        var plan = await world.PlanAsync($$""", "actions":[{{New("Planned " + world.Tag, planned)}}]""");

        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var reply = await CallToolAsync(token, "decide_retro_actions", $$"""{"project":"SHIP","sprint":"{{world.SprintName}}","plan":"{{plan}}","actions":[{{New("Other " + world.Tag, other)}}],"not_needed":[{"point":"{{other}}","reason":"r"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("created").GetArrayLength().ShouldBe(1);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE title = @title", new { title = "Other " + world.Tag })).ShouldBe(0);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE title = @title", new { title = "Planned " + world.Tag })).ShouldBe(1);
        (await connection.QuerySingleAsync<string?>("SELECT not_needed_reason FROM retro_point WHERE id = @other", new { other })).ShouldBeNull();
    }

    [Test]
    public async Task Apply_ShouldNeverMergeThemes()
    {
        var world = await WorldAsync();
        var from = await world.Seed.ThemeAsync($"From {world.Tag}");
        var into = await world.Seed.ThemeAsync($"Into {world.Tag}");
        var point = await world.PointAsync(RetroCategory.WentBadly, "Themed", themeId: from);
        var plan = await world.PlanAsync($$""", "merges":[{"from":"From {{world.Tag}}","into":"Into {{world.Tag}}","reason":"Same thing"}]""");

        var applied = await world.ApplyAsync(plan);

        applied.GetProperty("would_propose_merges").GetInt32().ShouldBe(1);
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<(string Status, Guid FromId, Guid IntoId)>("SELECT status, from_theme_id AS FromId, into_theme_id AS IntoId FROM retro_theme_merge_proposal WHERE from_theme_id = @from", new { from })).ShouldBe(("pending", from, into));
        (await connection.QuerySingleAsync<Guid?>("SELECT merged_into_id FROM retro_theme WHERE id = @from", new { from })).ShouldBeNull();
        (await connection.QuerySingleAsync<Guid?>("SELECT theme_id FROM retro_point WHERE id = @point", new { point })).ShouldBe(from);
    }

    [Test]
    public async Task Apply_WhenRunAgain_ShouldCreateNothingNew()
    {
        var world = await WorldAsync();
        var first = await world.PointAsync(RetroCategory.WentBadly, "First");
        var second = await world.PointAsync(RetroCategory.WentBadly, "Second");
        await world.Seed.ThemeAsync($"A {world.Tag}");
        await world.Seed.ThemeAsync($"B {world.Tag}");
        var plan = await world.PlanAsync($$""", "actions":[{{New("Fix " + world.Tag, first)}}],"not_needed":[{"point":"{{second}}","reason":"r"}],"merges":[{"from":"A {{world.Tag}}","into":"B {{world.Tag}}","reason":"r"}]""");
        await world.ApplyAsync(plan);
        var before = await SnapshotAsync();

        var again = await world.ApplyAsync(plan);

        again.GetProperty("created").GetArrayLength().ShouldBe(0);
        again.GetProperty("would_propose_merges").GetInt32().ShouldBe(0);
        Skipped(again).ShouldBe([first, second], ignoreOrder: true);
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Apply_WhenFiveNotNeeded_ShouldMarkAllFive()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(5);

        var applied = await world.ApplyAsync(await world.PlanAsync($$""", "not_needed":[{{NotNeededEntries(points)}}]"""));

        applied.GetProperty("would_mark_not_needed").GetArrayLength().ShouldBe(5);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retro_point WHERE id = ANY(@points) AND not_needed_reason = 'Not a problem' AND decided_at IS NOT NULL", new { points = points.ToArray() })).ShouldBe(5);
    }

    [Test]
    public async Task Apply_ShouldAssignThemesAsAgent()
    {
        var world = await WorldAsync();
        var plain = await world.PointAsync(RetroCategory.WentBadly, "Plain");
        var chosen = await world.Seed.ThemeAsync($"Chosen {world.Tag}");
        var byPerson = await world.PointAsync(RetroCategory.WentBadly, "By a person", themeId: chosen, themeSetBy: RetroThemeSetBy.Person);
        var lateOne = await world.PointAsync(RetroCategory.WentBadly, "Chosen after the preview");
        var body = $$""", "themes":[{"point":"{{plain}}","theme":"Agent theme {{world.Tag}}"},{"point":"{{byPerson}}","theme":"Other {{world.Tag}}"},{"point":"{{lateOne}}","theme":"Late {{world.Tag}}"}]""";
        var preview = await world.PreviewAsync(body);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE retro_point SET theme_id = @chosen, theme_set_by = 'person' WHERE id = @lateOne", new { chosen, lateOne });

        var applied = await world.ApplyAsync(Plan(preview));

        Skipped(preview).ShouldBe([byPerson]);
        applied.GetProperty("would_theme").GetInt32().ShouldBe(1);
        Skipped(applied).ShouldBe([lateOne]);
        (await connection.QuerySingleAsync<(string Name, string SetBy)>("SELECT t.name AS Name, p.theme_set_by AS SetBy FROM retro_point p JOIN retro_theme t ON t.id = p.theme_id WHERE p.id = @plain", new { plain })).ShouldBe(($"Agent theme {world.Tag}", "agent"));
        (await connection.QuerySingleAsync<(Guid? Theme, string SetBy)>("SELECT theme_id AS Theme, theme_set_by AS SetBy FROM retro_point WHERE id = @byPerson", new { byPerson })).ShouldBe((chosen, "person"));
    }

    [Test]
    public async Task Apply_WhenAPointsThemeChangedSincePreview_ShouldCreateTheActionWithThePreviewedTheme()
    {
        var world = await WorldAsync();
        var previewed = await world.Seed.ThemeAsync($"Previewed {world.Tag}");
        var changed = await world.Seed.ThemeAsync($"Changed {world.Tag}");
        var point = await world.PointAsync(RetroCategory.WentBadly, "Themed", themeId: previewed);
        var preview = await world.PreviewAsync($$""", "actions":[{{New("Themed " + world.Tag, point)}}]""");
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE retro_point SET theme_id = @changed WHERE id = @point", new { changed, point });

        var applied = await world.ApplyAsync(Plan(preview));

        var shown = preview.GetProperty("would_create")[0].GetProperty("theme").GetString();
        shown.ShouldBe($"Previewed {world.Tag}");
        var key = applied.GetProperty("created")[0].GetString();
        (await connection.QuerySingleAsync<string?>(
            "SELECT t.name FROM work_item w JOIN retro_action ra ON ra.work_item_id = w.id JOIN retro_theme t ON t.id = ra.theme_id WHERE w.key = @key", new { key })).ShouldBe(shown);
    }

    [Test]
    public async Task LeftUndecided_ShouldCountTheScopesUndecidedMinusThoseHandled()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(5);
        await world.PointAsync(RetroCategory.WentWell, "Good");
        await world.PointAsync(RetroCategory.WentBadly, "Decided", notNeededReason: "Fine");
        var body = $$""", "actions":[{{New("Fix " + world.Tag, points[0], points[1])}}],"not_needed":[{"point":"{{points[2]}}","reason":"r"}]""";

        var preview = await world.PreviewAsync(body);
        var applied = await world.ApplyAsync(Plan(preview));

        preview.GetProperty("left_undecided").GetInt32().ShouldBe(2);
        applied.GetProperty("left_undecided").GetInt32().ShouldBe(2);
    }

    private static string Plan(JsonElement preview) => preview.GetProperty("plan").GetString()!;

    private static string New(string title, params Guid[] points)
        => $$"""{"title":"{{title}}","fix_type":"skill","points":[{{string.Join(",", points.Select(id => $"\"{id}\""))}}]}""";

    private static string NotNeededEntries(IEnumerable<Guid> points)
        => string.Join(",", points.Select(id => $$"""{"point":"{{id}}","reason":"Not a problem"}"""));

    private static Guid[] Skipped(JsonElement result)
        => [.. result.GetProperty("skipped").EnumerateArray().Select(skip => skip.GetProperty("point").GetGuid())];

    private static string Agent() => $"agent-{Guid.NewGuid():N}";

    private Task<Scenario> WorldAsync() => Scenario.CreateAsync(this);

    private async Task<McpReply> PreviewRawAsync(string arguments) => await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "decide_retro_actions", arguments);

    private static async Task<(Guid Id, string Name)> SprintAsync(NpgsqlConnection connection)
    {
        var id = Guid.NewGuid();
        var name = $"Decide {id:N}";
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, @name, DATE '2001-01-01', DATE '2001-01-14')",
            new { id, project = SeedIds.Projects.Ship, name });
        return (id, name);
    }

    // Every row a decide run could touch, counted: unchanged means nothing was written.
    private async Task<string> SnapshotAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return string.Join(
            "|",
            await connection.QuerySingleAsync<string>(
                """
                SELECT (SELECT count(*) FROM work_item) || ',' || (SELECT count(*) FROM retro_action_point) || ',' || (SELECT count(*) FROM retro_theme) || ','
                    || (SELECT count(*) FROM retro_theme_merge_proposal) || ',' || (SELECT count(*) FROM retro_point WHERE not_needed_reason IS NOT NULL) || ','
                    || (SELECT coalesce(string_agg(coalesce(theme_id::text, '-'), ''), '') FROM (SELECT theme_id FROM retro_point ORDER BY id) t)
                """));
    }

    // One arranged sprint with a retro, and the calls that read it.
    private sealed class Scenario
    {
        private readonly DecideRetroActionsTests _tests;
        private readonly NpgsqlConnection _connection;
        private readonly Guid _retro;
        private int _position;

        private Scenario(DecideRetroActionsTests tests, NpgsqlConnection connection, (Guid Id, string Name) sprint, Guid retro)
        {
            _tests = tests;
            _connection = connection;
            _retro = retro;
            SprintName = sprint.Name;
            Seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        }

        public string SprintName { get; }

        public string Tag { get; } = Guid.NewGuid().ToString("N")[..8];

        public RetroSeed Seed { get; }

        public static async Task<Scenario> CreateAsync(DecideRetroActionsTests tests)
        {
            var connection = await tests.OpenDatabaseAsync();
            var sprint = await SprintAsync(connection);
            var retro = await new RetroSeed(connection, SeedIds.Projects.Ship).RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, SprintId: sprint.Id, AgentName: Agent()));
            return new Scenario(tests, connection, sprint, retro);
        }

        public Task<Guid> PointAsync(RetroCategory category, string text, string? notNeededReason = null, Guid? themeId = null, RetroThemeSetBy? themeSetBy = null)
            => Seed.PointAsync(_retro, new RetroSeedPoint(category, $"{text} {Tag}", ThemeId: themeId, ThemeSetBy: themeSetBy, Position: _position++, NotNeededReason: notNeededReason));

        public async Task<Guid[]> PointsAsync(int count)
        {
            var ids = new List<Guid>();
            for (var index = 0; index < count; index++)
            {
                ids.Add(await PointAsync(RetroCategory.WentBadly, $"Point {index}"));
            }

            return [.. ids];
        }

        public string NewActions(IEnumerable<Guid> points)
            => $$""", "actions":[{{string.Join(",", points.Select((id, index) => New($"New {index} {Tag}", id)))}}]""";

        public async Task<(Guid Id, string Key, string Title)> ExistingActionAsync(string title, string state, params Guid[] points)
        {
            var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle(title).WithStateNamed(state).InsertAsync(_connection);
            await Seed.ActionAsync(new RetroSeedAction(id, PointIds: points));
            return (id, await _connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }), title);
        }

        // Decided behind the tool's back: a person marked it Not needed.
        public Task DecideAsync(Guid point)
            => _connection.ExecuteAsync(
                "UPDATE retro_point SET not_needed_reason = 'Decided elsewhere', decided_by_user_id = @user, decided_at = now() WHERE id = @point", new { point, user = SeedIds.Users.Joel });

        public Task<McpReply> PreviewRawAsync(string body) => _tests.PreviewRawAsync(Arguments(body));

        public async Task<JsonElement> PreviewAsync(string body)
        {
            var reply = await PreviewRawAsync(body);
            reply.IsToolError().ShouldBeFalse(reply.Body);
            return reply.Structured();
        }

        public async Task<string> PlanAsync(string body) => Plan(await PreviewAsync(body));

        public async Task<JsonElement> ApplyAsync(string plan)
        {
            var token = await _tests.TokenAsync(TestUser.Joel, ReadWritePlan);
            var reply = await _tests.CallToolAsync(token, "decide_retro_actions", $$"""{"project":"SHIP","plan":"{{plan}}"}""");
            reply.IsToolError().ShouldBeFalse(reply.Body);
            return reply.Structured();
        }

        private string Arguments(string body)
            => $$"""{"project":"SHIP","sprint":"{{SprintName}}"{{(body.Length == 0 || body.TrimStart().StartsWith(',') ? string.Empty : ",")}}{{body}}}""";
    }

    [TestCase("actions", "title", "fix_type", "points")]
    [TestCase("not_needed", "point", "reason")]
    [TestCase("themes", "point", "theme")]
    [TestCase("merges", "from", "into", "reason")]
    public async Task ToolsList_ShouldRequireEveryNestedField(string argument, params string[] fields)
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var reply = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
        var tool = reply.Json().GetProperty("result").GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "decide_retro_actions");
        var schema = tool.GetProperty("inputSchema");

        var item = Resolve(schema.GetProperty("properties").GetProperty(argument).GetProperty("items"), schema);

        item.GetProperty("required").EnumerateArray().Select(name => name.GetString()).ShouldBe(fields, ignoreOrder: true);
        foreach (var field in fields)
        {
            var type = item.GetProperty("properties").GetProperty(field).GetProperty("type");
            // The generator lists every string as ["string","null"] (as for capture_plan's tasks), so only a Guid can be pinned non-null; the strings are checked at runtime below.
            if (field == "point")
            {
                type.ValueKind.ShouldBe(JsonValueKind.String, field);
            }
        }
    }

    [TestCase("""{"actions":[{"title":null,"fix_type":"skill","points":["PID"]}]}""", "actions[0].title")]
    [TestCase("""{"actions":[{"title":" ","fix_type":"skill","points":["PID"]}]}""", "actions[0].title")]
    [TestCase("""{"actions":[{"title":"t","fix_type":null,"points":["PID"]}]}""", "actions[0].fix_type")]
    [TestCase("""{"actions":[{"title":"t","fix_type":"nope","points":["PID"]}]}""", "actions[0].fix_type")]
    [TestCase("""{"actions":[{"title":"t","fix_type":"skill","points":null}]}""", "actions[0].points")]
    [TestCase("""{"actions":[{"title":"t","fix_type":"skill","points":[]}]}""", "actions[0].points")]
    [TestCase("""{"not_needed":[{"point":"PID","reason":null}]}""", "not_needed[0].reason")]
    [TestCase("""{"not_needed":[{"point":"PID","reason":" "}]}""", "not_needed[0].reason")]
    [TestCase("""{"themes":[{"point":"PID","theme":null}]}""", "themes[0].theme")]
    [TestCase("""{"merges":[{"from":null,"into":"b","reason":"r"}]}""", "merges[0].from")]
    [TestCase("""{"merges":[{"from":"a","into":null,"reason":"r"}]}""", "merges[0].into")]
    [TestCase("""{"merges":[{"from":"a","into":"b","reason":null}]}""", "merges[0].reason")]
    public async Task Decide_WhenANestedFieldIsMissing_ShouldBeValidationNamingIt(string nested, string field)
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var arguments = nested.Replace("PID", Guid.NewGuid().ToString()).Insert(1, "\"project\":\"SHIP\",\"sprint\":\"current\",");

        var reply = await CallToolAsync(token, "decide_retro_actions", arguments);

        ErrorField(reply).ShouldBe(("validation", field));
    }

    private static JsonElement Resolve(JsonElement node, JsonElement root)
    {
        if (!node.TryGetProperty("$ref", out var reference))
        {
            return node;
        }

        var current = root;
        foreach (var part in reference.GetString()!.TrimStart('#', '/').Split('/'))
        {
            current = current.GetProperty(part);
        }

        return current;
    }

    private static (string Code, string? Field) ErrorField(McpReply reply)
    {
        var error = reply.Structured().GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.TryGetProperty("field", out var field) ? field.GetString() : null);
    }

    private static string[] Names(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];
}
