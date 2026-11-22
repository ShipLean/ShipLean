using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Retros.Analysis;

// The whole flow on the real services: a scripted agent (no model) pages the export, previews and applies a record, reads the analysis back, and exports again.
// Each test arranges its own Feature, so the points of one never reach another.
[TestFixture]
public class RetroAnalysisJourneyTests : McpFixture
{
    private const int PageSize = 5;

    protected override bool WithProbeTools => false;

    [Test]
    public async Task Journey_ExportRecordGet_ShouldCreateTwoRankedActionsAndAReport()
    {
        var world = await ArrangeAsync();

        var run = await RunAsync(world);

        // Export: 9 live points (12 seeded, 2 actioned and 1 not needed left out), 5 a page.
        run.Pages.Count.ShouldBe(2);
        run.Pages[0].GetProperty("total_points").GetInt32().ShouldBe(9);
        run.Pages[0].GetProperty("next_cursor").GetString().ShouldNotBeNullOrEmpty();
        run.Pages[1].GetProperty("next_cursor").ValueKind.ShouldBe(JsonValueKind.Null);
        run.ExportedIds.Count.ShouldBe(9);
        run.ExportedIds.Distinct().Count().ShouldBe(9);
        run.ExportedIds.Intersect(world.Decided).ShouldBeEmpty();
        run.Pages[0].GetProperty("open_actions").GetArrayLength().ShouldBeGreaterThan(0);
        var commits = run.Pages.SelectMany(page => page.GetProperty("points").EnumerateArray()).Where(point => point.GetProperty("commits").GetArrayLength() > 0).ToList();
        commits.ShouldNotBeEmpty();
        commits.ShouldAllBe(point => point.GetProperty("commits")[0].GetProperty("sha").GetString() == world.Sha);

        // Record: the preview and the apply derive the same funnel.
        var funnel = run.Applied.GetProperty("funnel");
        funnel.GetProperty("points_in").GetInt32().ShouldBe(9);
        funnel.GetProperty("noise").GetInt32().ShouldBe(1);
        funnel.GetProperty("folded").GetInt32().ShouldBe(0);
        funnel.GetProperty("one_off").GetInt32().ShouldBe(1);
        funnel.GetProperty("generic").GetInt32().ShouldBe(7);
        funnel.GetProperty("clustered").GetInt32().ShouldBe(7);
        funnel.GetProperty("clustered_percent").GetDouble().ShouldBe(100.0);
        funnel.GetProperty("unclustered").GetInt32().ShouldBe(0);
        funnel.GetProperty("patterns_problem").GetInt32().ShouldBe(2);
        funnel.GetProperty("patterns_went_well").GetInt32().ShouldBe(1);
        funnel.GetProperty("fixes_proposed").GetInt32().ShouldBe(5);
        funnel.GetProperty("already_enforced_fully").GetInt32().ShouldBe(1);
        funnel.GetProperty("already_enforced_partly").GetInt32().ShouldBe(1);
        funnel.GetProperty("discarded").GetInt32().ShouldBe(1);
        funnel.GetProperty("actions_new").GetInt32().ShouldBe(2);
        funnel.GetProperty("actions_added").GetInt32().ShouldBe(0);
        run.Preview.GetProperty("funnel").GetRawText().ShouldBe(funnel.GetRawText());
        run.Preview.GetProperty("would_create").GetArrayLength().ShouldBe(2);
        run.Applied.GetProperty("created").GetArrayLength().ShouldBe(2);

        // Two actions, ranked: A's fix-tier fix first (priority 1, fix:fix), B's analyzer second (priority 3, fix:analyzer).
        var created = run.Applied.GetProperty("created").EnumerateArray().Select(key => key.GetString()!).ToList();
        var first = await PriorityAndTagsAsync(created[0]);
        first.Priority.ShouldBe(1);
        first.Tags.ShouldBe(["fix:fix"]);
        var second = await PriorityAndTagsAsync(created[1]);
        second.Priority.ShouldBe(3);
        second.Tags.ShouldBe(["fix:analyzer"]);

        // Get: the analysis, its patterns and every fix with its action.
        var analysis = run.Analysis;
        analysis.GetProperty("id").GetGuid().ShouldBe(run.Applied.GetProperty("analysis_id").GetGuid());
        analysis.GetProperty("embedding_model").GetString().ShouldBe("e5-journey");
        analysis.GetProperty("report_markdown").GetString().ShouldBe("# Journey report");
        analysis.GetProperty("funnel").GetRawText().ShouldBe(funnel.GetRawText());
        var patterns = analysis.GetProperty("patterns").EnumerateArray().ToList();
        patterns.Select(pattern => pattern.GetProperty("ref").GetString()).ShouldBe(["a", "b"]);
        var wentWell = analysis.GetProperty("went_well").EnumerateArray().Single();
        wentWell.GetProperty("ref").GetString().ShouldBe("w");
        wentWell.GetProperty("evidence").GetProperty("points").GetInt32().ShouldBe(3);

        var fixesA = Fixes(patterns[0]);
        fixesA.Select(f => f.Title).ShouldBe(["Fix the flaky run", "Guide line"], ignoreOrder: true);
        fixesA.ShouldAllBe(f => f.Action == created[0]);
        var fixesB = Fixes(patterns[1]).ToDictionary(f => f.Title);
        fixesB["Analyzer rule"].Action.ShouldBe(created[1]);
        fixesB.ContainsKey("Sounds right").ShouldBeFalse("a discarded fix is listed under discarded, not on its pattern");
        fixesB["Already tested"].Action.ShouldBeNull();

        var byTier = analysis.GetProperty("fixes_by_tier");
        byTier.GetProperty("fix").EnumerateArray().Single().GetProperty("action").GetProperty("key").GetString().ShouldBe(created[0]);
        byTier.GetProperty("analyzer").EnumerateArray().Single(f => f.GetProperty("title").GetString() == "Analyzer rule").GetProperty("action").GetProperty("key").GetString().ShouldBe(created[1]);
        var discarded = analysis.GetProperty("discarded").EnumerateArray().Single();
        discarded.GetProperty("title").GetString().ShouldBe("Sounds right");
        discarded.GetProperty("action").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Journey_AfterApply_ExportShouldLeaveTheActionedPointsOut()
    {
        var world = await ArrangeAsync();
        var run = await RunAsync(world);

        var again = await ExportPageAsync(world, null);

        var left = again.GetProperty("points").EnumerateArray().Select(point => point.GetProperty("id").GetGuid()).ToList();
        // Actioned by this apply: A's and B's points. Noise and one-off points are covered (D22). Only the went-well points remain, which have no decision.
        left.ShouldBe(world.WentWell, ignoreOrder: true);
        again.GetProperty("total_points").GetInt32().ShouldBe(3);
        run.Applied.GetProperty("skipped").GetArrayLength().ShouldBe(0);
        await using var connection = await OpenDatabaseAsync();
        var actioned = await connection.QuerySingleAsync<int>("SELECT count(DISTINCT point_id)::int FROM retro_action_point WHERE point_id = ANY(@points)", new { points = world.A.Concat(world.B).ToArray() });
        actioned.ShouldBe(4);
    }

    private async Task<Run> RunAsync(World world)
    {
        var pages = new List<JsonElement>();
        string? cursor = null;
        do
        {
            var page = await ExportPageAsync(world, cursor);
            pages.Add(page);
            cursor = page.GetProperty("next_cursor").GetString();
        }
        while (cursor is not null);

        var points = new List<object>();
        foreach (var id in world.A.Concat(world.B).Concat(world.WentWell))
        {
            points.Add(new { id, outcome = "clustered", generic = "A generic lesson." });
        }

        points.Add(new { id = world.Noise, outcome = "noise", generic = (string?)null });
        points.Add(new { id = world.OneOff, outcome = "oneOff", generic = "A one-off." });
        var patterns = new object[]
        {
            Pattern("a", "problem", world.A),
            Pattern("b", "problem", world.B),
            Pattern("w", "wentWell", world.WentWell),
        };
        var fixes = new object[]
        {
            Fix("a", "Guide line", "guide", "notCaught"),
            Fix("a", "Fix the flaky run", "fix", "notCaught"),
            Fix("b", "Analyzer rule", "analyzer", "partly"),
            Fix("b", "Sounds right", "test", "discarded"),
            Fix("b", "Already tested", "test", "fully"),
        };

        var preview = await RecordAsync(world, [.. points], patterns, fixes, null);
        var applied = await RecordAsync(world, [.. points], patterns, fixes, preview.GetProperty("plan").GetString());
        var read = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_retro_analysis", JsonSerializer.Serialize(new { project = "SHIP", id = applied.GetProperty("analysis_id").GetGuid() }));
        read.IsToolError().ShouldBeFalse(read.Body);
        return new Run(pages, [.. pages.SelectMany(page => page.GetProperty("points").EnumerateArray()).Select(point => point.GetProperty("id").GetGuid())], preview, applied, read.Structured());
    }

    private async Task<JsonElement> ExportPageAsync(World world, string? cursor)
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "export_retro_points", JsonSerializer.Serialize(new { project = "SHIP", feature = world.FeatureKey, limit = PageSize, cursor }));
        reply.IsToolError().ShouldBeFalse(reply.Body);
        return reply.Structured();
    }

    private async Task<JsonElement> RecordAsync(World world, object[] points, object[] patterns, object[] fixes, string? plan)
    {
        var arguments = JsonSerializer.Serialize(new
        {
            project = "SHIP", feature = world.FeatureKey, embedding_model = "e5-journey", rewrite_model = "scripted", report_markdown = "# Journey report", points, patterns, fixes, plan,
        });
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, plan is null ? ReadOnly : ReadWritePlan), "record_retro_analysis", arguments);
        reply.IsToolError().ShouldBeFalse(reply.Body);
        return reply.Structured();
    }

    private async Task<(int Priority, string[] Tags)> PriorityAndTagsAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<(int, string[])>("SELECT priority, tags FROM work_item WHERE key = @key", new { key });
    }

    private static object Pattern(string reference, string kind, Guid[] points)
        => new { @ref = reference, kind, title = $"{reference} title", lesson = $"{reference} lesson", asks = new[] { "Ask one" }, point_ids = points };

    private static object Fix(string pattern, string title, string type, string enforcement)
        => new { pattern_ref = pattern, title, fix_type = type, enforcement, enforced_by = enforcement is "partly" or "fully" ? "CI" : null, reason = "Because." };

    private static List<(string Title, string? Action)> Fixes(JsonElement pattern)
        => [.. pattern.GetProperty("fixes").EnumerateArray().Select(f => (f.GetProperty("title").GetString()!, f.GetProperty("action").ValueKind == JsonValueKind.Null ? null : f.GetProperty("action").GetProperty("key").GetString()))];

    // A Feature with two stories, each with a sub-agent and a conductor retro; 12 points: 3 went well, 2 already actioned, 1 not needed; one commit on a task of the first story.
    private async Task<World> ArrangeAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = SeedIds.Projects.Ship;
        var feature = await new WorkItemBuilder(project).WithType("feature").WithTitle("Journey feature").InsertAsync(connection);
        var storyOne = await new WorkItemBuilder(project).WithType("story").WithParent(feature).InsertAsync(connection);
        var storyTwo = await new WorkItemBuilder(project).WithType("story").WithParent(feature).InsertAsync(connection);
        var task = await new WorkItemBuilder(project).WithType("task").WithParent(storyOne).InsertAsync(connection);
        var featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        var tag = Guid.NewGuid().ToString("N")[..8];
        var sha = Guid.NewGuid().ToString("N") + tag;
        var repository = Guid.NewGuid();
        await connection.ExecuteAsync("INSERT INTO code_repository (id, provider, host, path) VALUES (@repository, 'github', 'github.com', @path)", new { repository, path = $"acme/{repository:N}" });
        await connection.ExecuteAsync(
            """
            INSERT INTO code_commit (repository_id, sha, parent_shas, subject, author_name, authored_at, additions, deletions, files_changed, paths, read_at)
            VALUES (@repository, @sha, '{}', 'A commit', 'Author', now(), 1, 1, 1, '{a.cs}', now())
            """,
            new { repository, sha });
        await connection.ExecuteAsync(
            "INSERT INTO work_item_commit (id, work_item_id, repository_id, sha, position, added_via) VALUES (@id, @task, @repository, @sha, 1, 'form')",
            new { id = Guid.NewGuid(), task, repository, sha });

        var seed = new RetroSeed(connection, project);
        var oneAgent = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: storyOne, AgentName: $"sub-one-{tag}"));
        var oneConductor = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Conductor, storyOne, AgentName: $"lead-one-{tag}"));
        var twoAgent = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: storyTwo, AgentName: $"sub-two-{tag}"));
        var twoConductor = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Conductor, storyTwo, AgentName: $"lead-two-{tag}"));

        var a = new[] { await BadAsync(seed, oneAgent, "A1"), await BadAsync(seed, twoAgent, "A2") };
        var b = new[] { await BadAsync(seed, oneConductor, "B1"), await BadAsync(seed, twoConductor, "B2") };
        var noise = await BadAsync(seed, oneAgent, "Noise");
        var oneOff = await BadAsync(seed, twoAgent, "One off");
        var wentWell = new[]
        {
            await seed.PointAsync(oneAgent, new RetroSeedPoint(RetroCategory.WentWell, $"Well 1 {tag}")),
            await seed.PointAsync(twoAgent, new RetroSeedPoint(RetroCategory.WentWell, $"Well 2 {tag}")),
            await seed.PointAsync(twoConductor, new RetroSeedPoint(RetroCategory.WentWell, $"Well 3 {tag}")),
        };
        var actioned = new[] { await BadAsync(seed, oneAgent, "Actioned 1"), await BadAsync(seed, twoAgent, "Actioned 2") };
        var action = await new WorkItemBuilder(project).WithType("action").WithTitle($"Open {tag}").WithStateNamed("New").InsertAsync(connection);
        await seed.ActionAsync(new RetroSeedAction(action, PointIds: actioned));
        var notNeeded = await seed.PointAsync(oneConductor, new RetroSeedPoint(RetroCategory.Improve, $"Not needed {tag}", NotNeededReason: "Decided elsewhere"));

        return new World(featureKey, sha, a, b, noise, oneOff, wentWell, [.. actioned, notNeeded]);
    }

    private static Task<Guid> BadAsync(RetroSeed seed, Guid retro, string text)
        => seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"{text} {Guid.NewGuid():N}"));

    private sealed record World(string FeatureKey, string Sha, Guid[] A, Guid[] B, Guid Noise, Guid OneOff, Guid[] WentWell, Guid[] Decided);

    private sealed record Run(List<JsonElement> Pages, List<Guid> ExportedIds, JsonElement Preview, JsonElement Applied, JsonElement Analysis);
}
