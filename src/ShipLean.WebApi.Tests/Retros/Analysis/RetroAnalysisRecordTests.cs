using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Retros.Analysis;

// record_retro_analysis (C2) over the real writer: what a preview derives from the database, what an apply creates and stores, and what it skips.
// Each scenario arranges its own sprint, so the points of one never reach another. The priority and tag of an action are decide's, so they are not asserted here.
[TestFixture]
public class RetroAnalysisRecordTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public async Task Preview_ShouldDeriveTheFunnelFromPointOutcomes()
    {
        var world = await WorldAsync();
        var noise = await world.PointAsync("Noise");
        var folded = await world.PointAsync("Folded");
        var oneOff = await world.PointAsync("One off");
        var first = await world.PointAsync("First");
        var second = await world.PointAsync("Second");
        var loose = await world.PointAsync("Loose");
        var unsent = await world.PointAsync("Unsent");

        var result = await world.PreviewAsync(
            [Pt(noise, "noise", null), Pt(folded, "folded", null, noise), Pt(oneOff, "oneOff"), Pt(first, "clustered"), Pt(second, "clustered"), Pt(loose, "unclustered")],
            [Pat("p1", "problem", first, second)],
            [Fx("p1", "Add a check", "analyzer")]);

        var funnel = result.GetProperty("funnel");
        funnel.GetProperty("points_in").GetInt32().ShouldBe(7);
        funnel.GetProperty("noise").GetInt32().ShouldBe(1);
        funnel.GetProperty("folded").GetInt32().ShouldBe(1);
        funnel.GetProperty("one_off").GetInt32().ShouldBe(1);
        funnel.GetProperty("generic").GetInt32().ShouldBe(3);
        funnel.GetProperty("clustered").GetInt32().ShouldBe(2);
        funnel.GetProperty("clustered_percent").GetDouble().ShouldBe(66.7);
        funnel.GetProperty("unclustered").GetInt32().ShouldBe(1);
        funnel.GetProperty("patterns_problem").GetInt32().ShouldBe(1);
        funnel.GetProperty("fixes_proposed").GetInt32().ShouldBe(1);
        funnel.GetProperty("actions_new").GetInt32().ShouldBe(1);
        result.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldContain(w => w!.Contains(unsent.ToString(), StringComparison.Ordinal));
    }

    [Test]
    public async Task Preview_ShouldComputeEvidenceFromTheDatabase()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").InsertAsync(connection);
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithParent(feature).InsertAsync(connection);
        var taskOne = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithParent(story).InsertAsync(connection);
        var taskTwo = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithParent(story).InsertAsync(connection);
        var world = await WorldAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retroOne = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: taskOne, SprintId: world.SprintId, AgentName: "alpha"));
        var retroTwo = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: taskTwo, SprintId: world.SprintId, AgentName: "beta"));
        var early = DateTimeOffset.UnixEpoch.AddDays(20000);
        var late = early.AddDays(3);
        var one = await seed.PointAsync(retroOne, new RetroSeedPoint(RetroCategory.WentBadly, "One", CreatedAt: early));
        var two = await seed.PointAsync(retroTwo, new RetroSeedPoint(RetroCategory.WentBadly, "Two", CreatedAt: late));
        var three = await seed.PointAsync(retroOne, new RetroSeedPoint(RetroCategory.Improve, "Three", CreatedAt: early.AddDays(1)));

        var result = await world.PreviewAsync(
            [Pt(one, "clustered"), Pt(two, "clustered"), Pt(three, "clustered")], [Pat("p1", "problem", one, two, three)], [Fx("p1", "Add a check", "analyzer")]);

        var evidence = result.GetProperty("patterns")[0].GetProperty("evidence");
        evidence.GetProperty("points").GetInt32().ShouldBe(3);
        Strings(evidence, "items").ShouldBe([await KeyAsync(connection, taskOne), await KeyAsync(connection, taskTwo)], ignoreOrder: true);
        Strings(evidence, "features").ShouldBe([await KeyAsync(connection, feature)]);
        Strings(evidence, "sprints").ShouldBe([world.SprintName]);
        Strings(evidence, "agents").ShouldBe(["alpha", "beta"], ignoreOrder: true);
        evidence.GetProperty("first_logged").GetDateTimeOffset().ShouldBe(early);
        evidence.GetProperty("last_logged").GetDateTimeOffset().ShouldBe(late);
    }

    [Test]
    public async Task Preview_WhenPointOutOfScopeOrDecided_ShouldNamePointsIndex()
    {
        var world = await WorldAsync();
        var good = await world.PointAsync("Good");
        var decided = await world.PointAsync("Decided");
        await world.DecideAsync(decided);

        var stranger = await world.PreviewRawAsync([Pt(Guid.NewGuid(), "noise", null)], [], []);
        var settled = await world.PreviewRawAsync([Pt(good, "noise", null), Pt(decided, "noise", null)], [], []);

        ErrorField(stranger).ShouldBe(("validation", "points[0].id"));
        ErrorField(settled).ShouldBe(("validation", "points[1].id"));
    }

    [Test]
    public async Task Preview_WhenPatternPointNotClustered_ShouldBeValidation()
    {
        var world = await WorldAsync();
        var point = await world.PointAsync("Point");

        var reply = await world.PreviewRawAsync([Pt(point, "unclustered")], [Pat("p1", "problem", point)], []);

        ErrorField(reply).ShouldBe(("validation", "patterns[0].point_ids"));
    }

    [Test]
    public async Task Preview_ShouldRankFixesByTierThenSize()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(11);

        var result = await world.PreviewAsync(
            [.. points.Select(id => Pt(id, "clustered"))],
            [
                Pat("goodWell", "wentWell", points[0]),
                Pat("test3", "problem", points[1], points[2], points[3]),
                Pat("enforced", "problem", points[4]),
                Pat("analyzer2", "problem", points[5], points[6]),
                Pat("analyzer3", "problem", points[7], points[8], points[9]),
                Pat("structure1", "problem", points[10]),
            ],
            [Fx("test3", "Test it", "test"), Fx("enforced", "Already done", "fix", "fully"), Fx("analyzer2", "Analyse two", "analyzer"), Fx("analyzer3", "Analyse three", "analyzer"), Fx("structure1", "Restructure", "structure")]);

        var ranked = result.GetProperty("patterns").EnumerateArray().Select(p => (Ref: p.GetProperty("ref").GetString(), Rank: p.GetProperty("rank").GetInt32())).ToList();
        ranked.ShouldBe([("structure1", 1), ("analyzer3", 2), ("analyzer2", 3), ("test3", 4), ("enforced", 5), ("goodWell", 6)]);
        result.GetProperty("would_create").EnumerateArray().Select(l => l.GetProperty("title").GetString()).ShouldBe(["Restructure", "Analyse three", "Analyse two", "Test it"]);
    }

    [Test]
    public async Task Apply_ShouldStoreAnalysisPatternsFixesAndPoints()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(3);
        var noise = await world.PointAsync("Noise");
        var points4 = new[] { points[0], points[1], points[2], noise };

        var applied = await world.ApplyAsync(
            [Pt(points[0], "clustered"), Pt(points[1], "clustered"), Pt(points[2], "unclustered"), Pt(noise, "noise", null)],
            [Pat("p1", "problem", points[0], points[1])],
            [Fx("p1", "Guide line", "guide"), Fx("p1", "Analyser", "analyzer")]);

        await using var connection = await OpenDatabaseAsync();
        var analysis = Guid.Parse(applied.GetProperty("analysis_id").GetString()!);
        var row = await connection.QuerySingleAsync<(string Kind, Guid Sprint, string Model, string? Rewrite, string Report)>(
            "SELECT scope_kind, sprint_id, embedding_model, rewrite_model, report_markdown FROM retro_analysis WHERE id = @analysis", new { analysis });
        row.ShouldBe(("sprint", world.SprintId, "e5-test", "writer-test", "# Report"));
        var pattern = await connection.QuerySingleAsync<(Guid Id, string Title, int Rank, string[] Asks)>("SELECT id, title, rank, asks FROM retro_pattern WHERE analysis_id = @analysis", new { analysis });
        pattern.Title.ShouldBe("p1 title");
        pattern.Rank.ShouldBe(1);
        pattern.Asks.ShouldBe(["Ask one"]);
        var stored = (await connection.QueryAsync<(Guid Id, string Outcome, Guid? Pattern)>(
            "SELECT point_id, outcome, pattern_id FROM retro_analysis_point WHERE analysis_id = @analysis", new { analysis })).ToDictionary(r => r.Id);
        stored.Keys.ShouldBe(points4, ignoreOrder: true);
        stored[points[0]].Pattern.ShouldBe(pattern.Id);
        stored[points[1]].Pattern.ShouldBe(pattern.Id);
        stored[points[2]].Pattern.ShouldBeNull();
        stored[noise].Outcome.ShouldBe("noise");
        var fixes = (await connection.QueryAsync<(string Title, int Rank)>("SELECT title, rank FROM retro_pattern_fix WHERE pattern_id = @id ORDER BY rank", new { pattern.Id })).ToList();
        fixes.ShouldBe([("Analyser", 1), ("Guide line", 2)]);
    }

    [Test]
    public async Task Apply_ShouldCreateOneActionPerProblemPatternAndMarkPointsActioned()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(4);

        var applied = await world.ApplyAsync(
            [.. points.Select(id => Pt(id, "clustered"))], [Pat("a", "problem", points[0], points[1]), Pat("b", "problem", points[2], points[3])],
            [Fx("a", "Fix a", "analyzer"), Fx("b", "Fix b", "test")]);

        applied.GetProperty("created").GetArrayLength().ShouldBe(2);
        applied.GetProperty("funnel").GetProperty("actions_new").GetInt32().ShouldBe(2);
        await using var connection = await OpenDatabaseAsync();
        await StoredFunnelShouldMatchAsync(connection, applied);
        var actioned = await connection.QuerySingleAsync<int>("SELECT count(DISTINCT point_id)::int FROM retro_action_point WHERE point_id = ANY(@points)", new { points });
        actioned.ShouldBe(4);
    }

    [Test]
    public async Task Apply_WhenFixFullyEnforcedOrDiscarded_ShouldCreateNoAction()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(2);

        var applied = await world.ApplyAsync(
            [.. points.Select(id => Pt(id, "clustered"))], [Pat("a", "problem", points[0]), Pat("b", "problem", points[1])],
            [Fx("a", "Done already", "analyzer", "fully"), Fx("b", "Not a fix", "guide", "discarded")]);

        applied.GetProperty("created").GetArrayLength().ShouldBe(0);
        applied.GetProperty("would_create").GetArrayLength().ShouldBe(0);
        applied.GetProperty("analysis_id").GetString().ShouldNotBeNullOrEmpty();
        var funnel = applied.GetProperty("funnel");
        funnel.GetProperty("already_enforced_fully").GetInt32().ShouldBe(1);
        funnel.GetProperty("discarded").GetInt32().ShouldBe(1);
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM retro_action_point WHERE point_id = ANY(@points)", new { points })).ShouldBe(0);
        (await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM retro_pattern_fix WHERE action_work_item_id IS NOT NULL AND title IN ('Done already', 'Not a fix')")).ShouldBe(0);
    }

    [Test]
    public async Task Apply_WhenExistingGiven_ShouldAddPointsToThatItem()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(3);
        var existing = await world.ExistingActionAsync(points[2]);

        var applied = await world.ApplyAsync(
            [.. points.Take(2).Select(id => Pt(id, "clustered"))], [Pat("a", "problem", points[0], points[1])], [Fx("a", "Extend", "analyzer", existing: existing.Key)]);

        applied.GetProperty("created").GetArrayLength().ShouldBe(0);
        applied.GetProperty("would_add")[0].GetProperty("existing").GetString().ShouldBe(existing.Key);
        applied.GetProperty("funnel").GetProperty("actions_added").GetInt32().ShouldBe(1);
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM retro_action_point WHERE work_item_id = @id", new { id = existing.Id })).ShouldBe(3);
    }

    [Test]
    public async Task Apply_WhenCreatingAndAdding_ShouldStoreTheFunnelItReturns()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(5);
        var existing = await world.ExistingActionAsync(points[4]);

        var applied = await world.ApplyAsync(
            [.. points.Take(4).Select(id => Pt(id, "clustered"))], [Pat("a", "problem", points[0], points[1]), Pat("b", "problem", points[2], points[3])],
            [Fx("a", "Extend", "analyzer", existing: existing.Key), Fx("b", "New one", "guide")]);

        var funnel = applied.GetProperty("funnel");
        funnel.GetProperty("points_in").GetInt32().ShouldBe(4);
        funnel.GetProperty("actions_new").GetInt32().ShouldBe(1);
        funnel.GetProperty("actions_added").GetInt32().ShouldBe(1);
        await using var connection = await OpenDatabaseAsync();
        await StoredFunnelShouldMatchAsync(connection, applied);
    }

    [Test]
    public async Task Apply_WhenPointDecidedSincePreview_ShouldSkipItAndStillStore()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(2);
        var points2 = new[] { Pt(points[0], "clustered"), Pt(points[1], "clustered") };
        var patterns = new[] { Pat("a", "problem", points[0], points[1]) };
        var fixes = new[] { Fx("a", "Fix a", "analyzer") };
        var plan = await world.PlanAsync(points2, patterns, fixes);
        await world.DecideAsync(points[1]);

        var applied = await world.ApplyPlanAsync(plan, points2, patterns, fixes);

        applied.GetProperty("skipped").EnumerateArray().Select(s => s.GetProperty("point").GetGuid()).ShouldBe([points[1]]);
        applied.GetProperty("analysis_id").GetString().ShouldNotBeNullOrEmpty();
        applied.GetProperty("would_create")[0].GetProperty("covers").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task Preview_WhenWentWellPointsAndPattern_ShouldAcceptAndCreateNoAction()
    {
        var world = await WorldAsync();
        var well = await world.PointAsync("Went well", RetroCategory.WentWell);
        var other = await world.PointAsync("Also well", RetroCategory.WentWell);

        var result = await world.PreviewAsync([Pt(well, "clustered"), Pt(other, "clustered")], [Pat("w", "wentWell", well, other)], []);

        result.GetProperty("would_create").GetArrayLength().ShouldBe(0);
        result.GetProperty("funnel").GetProperty("patterns_went_well").GetInt32().ShouldBe(1);
        result.GetProperty("funnel").GetProperty("points_in").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task Apply_WhenPatternHasTwoActingFixes_ShouldCreateOneActionAndLinkBothFixes()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(2);

        var applied = await world.ApplyAsync(
            [.. points.Select(id => Pt(id, "clustered"))], [Pat("a", "problem", points[0], points[1])],
            [Fx("a", "Guide line", "guide"), Fx("a", "Analyser", "analyzer", "partly"), Fx("a", "Covered", "test", "fully")]);

        applied.GetProperty("created").GetArrayLength().ShouldBe(1);
        applied.GetProperty("funnel").GetProperty("actions_new").GetInt32().ShouldBe(1);
        applied.GetProperty("would_create")[0].GetProperty("title").GetString().ShouldBe("Analyser");
        await using var connection = await OpenDatabaseAsync();
        var analysis = Guid.Parse(applied.GetProperty("analysis_id").GetString()!);
        var fixes = (await connection.QueryAsync<(string Title, Guid? Action)>(
            "SELECT f.title, f.action_work_item_id FROM retro_pattern_fix f JOIN retro_pattern p ON p.id = f.pattern_id WHERE p.analysis_id = @analysis", new { analysis })).ToDictionary(f => f.Title);
        fixes["Analyser"].Action.ShouldNotBeNull();
        fixes["Guide line"].Action.ShouldBe(fixes["Analyser"].Action);
        fixes["Covered"].Action.ShouldBeNull();
    }

    [Test]
    public async Task Preview_WhenContinuesOtherModel_ShouldNamePatternsContinues()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(3);
        var firstRun = await world.ApplyAsync([Pt(points[0], "clustered")], [Pat("a", "problem", points[0])], [Fx("a", "Fix a", "analyzer", "fully")]);
        await using var connection = await OpenDatabaseAsync();
        var earlier = await connection.QuerySingleAsync<Guid>(
            "SELECT id FROM retro_pattern WHERE analysis_id = @analysis", new { analysis = Guid.Parse(firstRun.GetProperty("analysis_id").GetString()!) });

        var otherModel = await world.PreviewRawAsync([Pt(points[1], "clustered")], [Continuing("b", points[1], earlier)], [], model: "another-model");
        var sameModel = await world.PreviewAsync([Pt(points[1], "clustered")], [Continuing("b", points[1], earlier)], []);

        ErrorField(otherModel).ShouldBe(("validation", "patterns[0].continues"));
        sameModel.GetProperty("patterns")[0].GetProperty("continues").GetGuid().ShouldBe(earlier);
    }

    [Test]
    public async Task Apply_WhenFiveNewActions_ShouldCreateAllFive()
    {
        var world = await WorldAsync();
        var points = await world.PointsAsync(5);

        var applied = await world.ApplyAsync(
            [.. points.Select(id => Pt(id, "clustered"))], [.. points.Select((id, index) => Pat($"p{index}", "problem", id))],
            [.. points.Select((_, index) => Fx($"p{index}", $"Fix {index}", "guide"))]);

        applied.GetProperty("created").GetArrayLength().ShouldBe(5);
        applied.GetProperty("funnel").GetProperty("actions_new").GetInt32().ShouldBe(5);
    }

    private static async Task StoredFunnelShouldMatchAsync(NpgsqlConnection connection, JsonElement applied)
    {
        var funnel = applied.GetProperty("funnel");
        var stored = await connection.QuerySingleAsync<(int PointsIn, int ActionsNew, int ActionsAdded)>(
            "SELECT points_in, actions_new, actions_added FROM retro_analysis WHERE id = @id", new { id = Guid.Parse(applied.GetProperty("analysis_id").GetString()!) });
        stored.ShouldBe((funnel.GetProperty("points_in").GetInt32(), funnel.GetProperty("actions_new").GetInt32(), funnel.GetProperty("actions_added").GetInt32()));
    }

    private Task<Scenario> WorldAsync() => Scenario.CreateAsync(this);

    private static object Pt(Guid id, string outcome, string? generic = "A generic lesson.", Guid? foldedInto = null)
        => new { id, outcome, generic, folded_into = foldedInto };

    private static object Pat(string reference, string kind, params Guid[] points) => Pattern(reference, kind, points, null);

    private static object Continuing(string reference, Guid point, Guid earlier) => Pattern(reference, "problem", [point], earlier);

    private static object Pattern(string reference, string kind, Guid[] points, Guid? continues)
        => new { @ref = reference, kind, title = $"{reference} title", lesson = $"{reference} lesson", asks = new[] { "Ask one" }, point_ids = points, continues };

    private static object Fx(string pattern, string title, string type, string enforcement = "notCaught", string? existing = null)
        => new { pattern_ref = pattern, title, fix_type = type, enforcement, enforced_by = enforcement is "partly" or "fully" ? "CI" : null, reason = "Because.", existing };

    private static string[] Strings(JsonElement element, string name) => [.. element.GetProperty(name).EnumerateArray().Select(item => item.GetString()!)];

    private static async Task<string> KeyAsync(NpgsqlConnection connection, Guid id) => await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });

    private static (string Code, string? Field) ErrorField(McpReply reply)
    {
        var error = reply.Structured().GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.TryGetProperty("field", out var field) ? field.GetString() : null);
    }

    // One arranged sprint with a retro, and the calls that preview and apply an analysis of it.
    private sealed class Scenario
    {
        private readonly RetroAnalysisRecordTests _tests;
        private readonly NpgsqlConnection _connection;
        private readonly Guid _retro;
        private int _position;

        private Scenario(RetroAnalysisRecordTests tests, NpgsqlConnection connection, Guid sprintId, string sprintName, Guid retro)
        {
            _tests = tests;
            _connection = connection;
            _retro = retro;
            SprintId = sprintId;
            SprintName = sprintName;
        }

        public Guid SprintId { get; }

        public string SprintName { get; }

        private string Tag { get; } = Guid.NewGuid().ToString("N")[..8];

        public static async Task<Scenario> CreateAsync(RetroAnalysisRecordTests tests)
        {
            var connection = await tests.OpenDatabaseAsync();
            var id = Guid.NewGuid();
            var name = $"Record {id:N}";
            await connection.ExecuteAsync(
                "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, @name, DATE '2001-01-01', DATE '2001-01-14')", new { id, project = SeedIds.Projects.Ship, name });
            var retro = await new RetroSeed(connection, SeedIds.Projects.Ship).RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, SprintId: id, AgentName: $"agent-{Guid.NewGuid():N}"));
            return new Scenario(tests, connection, id, name, retro);
        }

        public Task<Guid> PointAsync(string text, RetroCategory category = RetroCategory.WentBadly)
            => new RetroSeed(_connection, SeedIds.Projects.Ship).PointAsync(_retro, new RetroSeedPoint(category, $"{text} {Tag}", Position: _position++));

        public async Task<Guid[]> PointsAsync(int count)
        {
            var ids = new List<Guid>();
            for (var index = 0; index < count; index++)
            {
                ids.Add(await PointAsync($"Point {index}"));
            }

            return [.. ids];
        }

        public Task DecideAsync(Guid point)
            => _connection.ExecuteAsync(
                "UPDATE retro_point SET not_needed_reason = 'Decided elsewhere', decided_by_user_id = @user, decided_at = now() WHERE id = @point", new { point, user = SeedIds.Users.Joel });

        public async Task<(Guid Id, string Key)> ExistingActionAsync(params Guid[] points)
        {
            var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle($"Open {Tag}").WithStateNamed("New").InsertAsync(_connection);
            await new RetroSeed(_connection, SeedIds.Projects.Ship).ActionAsync(new RetroSeedAction(id, PointIds: points));
            return (id, await KeyAsync(_connection, id));
        }

        public async Task<McpReply> PreviewRawAsync(object[] points, object[] patterns, object[] fixes, string model = "e5-test")
            => await _tests.CallToolAsync(await _tests.TokenAsync(TestUser.Joel, ReadOnly), "record_retro_analysis", Arguments(points, patterns, fixes, null, model));

        public async Task<JsonElement> PreviewAsync(object[] points, object[] patterns, object[] fixes)
        {
            var reply = await PreviewRawAsync(points, patterns, fixes);
            reply.IsToolError().ShouldBeFalse(reply.Body);
            return reply.Structured();
        }

        public async Task<string> PlanAsync(object[] points, object[] patterns, object[] fixes) => (await PreviewAsync(points, patterns, fixes)).GetProperty("plan").GetString()!;

        public async Task<JsonElement> ApplyAsync(object[] points, object[] patterns, object[] fixes)
            => await ApplyPlanAsync(await PlanAsync(points, patterns, fixes), points, patterns, fixes);

        public async Task<JsonElement> ApplyPlanAsync(string plan, object[] points, object[] patterns, object[] fixes)
        {
            var reply = await _tests.CallToolAsync(await _tests.TokenAsync(TestUser.Joel, ReadWritePlan), "record_retro_analysis", Arguments(points, patterns, fixes, plan, "e5-test"));
            reply.IsToolError().ShouldBeFalse(reply.Body);
            return reply.Structured();
        }

        private string Arguments(object[] points, object[] patterns, object[] fixes, string? plan, string model)
            => JsonSerializer.Serialize(new
            {
                project = "SHIP", sprint = SprintName, embedding_model = model, rewrite_model = "writer-test", report_markdown = "# Report", points, patterns, fixes, plan,
            });
    }
}
