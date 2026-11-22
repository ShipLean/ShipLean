using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Retros.Analysis;

// export_retro_points (C1) on the real export service: scope, the undecided and covered filters, hydration, paging. Each test arranges its own Feature, sprint or far-past days,
// so the points of one never reach another.
[TestFixture]
public class RetroExportTests : McpFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    protected override bool WithProbeTools => false;

    [Test]
    public async Task Export_WhenFeatureScope_ShouldIncludeChildStoryAndBugPoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var story = await ItemAsync(connection, "story", feature);
        var bug = await ItemAsync(connection, "bug", feature);
        var task = await ItemAsync(connection, "task", story);
        var stranger = await ItemAsync(connection, "story");
        await PointOnAsync(connection, feature, "On the feature");
        await PointOnAsync(connection, story, "On the story");
        await PointOnAsync(connection, bug, "On the bug");
        await PointOnAsync(connection, task, "On the task");
        await PointOnAsync(connection, stranger, "On a stranger");

        var result = await ExportAsync($$"""{"project":"SHIP","feature":"{{await KeyAsync(connection, feature)}}"}""");

        Texts(result).ShouldBe(["On the feature", "On the story", "On the bug"], ignoreOrder: true);
        result.GetProperty("total_points").GetInt32().ShouldBe(3);
        result.GetProperty("scope").GetProperty("kind").GetString().ShouldBe("feature");
    }

    [Test]
    public async Task Export_WhenPointActionedOrNotNeeded_ShouldLeaveItOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: feature, AgentName: Agent()));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Undecided"));
        var actioned = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Actioned"));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Not needed", NotNeededReason: "Not a problem"));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Archived", Archived: true));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Went well"));
        await seed.ActionAsync(new RetroSeedAction(await ItemAsync(connection, "action"), PointId: actioned));

        var result = await ExportAsync($$"""{"project":"SHIP","feature":"{{await KeyAsync(connection, feature)}}"}""");

        Texts(result).ShouldBe(["Undecided", "Went well"], ignoreOrder: true);
    }

    [Test]
    public async Task Export_WhenIncludeWentWellFalse_ShouldLeaveOutWentWell()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: feature, AgentName: Agent()));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Bad"));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Good"));
        var key = await KeyAsync(connection, feature);

        var without = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}","include_went_well":false}""");
        var with = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}"}""");

        Texts(without).ShouldBe(["Bad"]);
        without.GetProperty("total_points").GetInt32().ShouldBe(1);
        Texts(with).ShouldBe(["Bad", "Good"], ignoreOrder: true);
    }

    [Test]
    public async Task Export_WhenRangeScope_ShouldUseTheDayLoggedInclusive()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: await ItemAsync(connection, "task"), AgentName: Agent()));
        var utc = TimeSpan.Zero;
        var edges = new[]
        {
            new DateTimeOffset(1994, 3, 9, 23, 59, 59, utc),
            new DateTimeOffset(1994, 3, 10, 0, 0, 0, utc),
            new DateTimeOffset(1994, 3, 11, 12, 0, 0, utc),
            new DateTimeOffset(1994, 3, 12, 23, 59, 59, utc),
            new DateTimeOffset(1994, 3, 13, 0, 0, 0, utc),
        };
        foreach (var at in edges)
        {
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"At {at:O}", CreatedAt: at));
        }

        var result = await ExportAsync("""{"project":"SHIP","from":"1994-03-10","to":"1994-03-12"}""");

        Texts(result).Count.ShouldBe(3);
        Texts(result).ShouldAllBe(text => !text.StartsWith("At 1994-03-09", StringComparison.Ordinal) && !text.StartsWith("At 1994-03-13", StringComparison.Ordinal));
        result.GetProperty("scope").GetProperty("kind").GetString().ShouldBe("range");
        result.GetProperty("scope").GetProperty("from").GetString().ShouldBe("1994-03-10");
        result.GetProperty("scope").GetProperty("to").GetString().ShouldBe("1994-03-12");
    }

    [Test]
    public async Task Export_WhenPagedByTwo_ShouldReturnEveryPointOnceInOrder()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: feature, AgentName: Agent()));
        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        var shared = now.AddDays(-2);
        var times = new[] { now.AddDays(-5), shared, shared, shared, now.AddDays(-1) };
        for (var index = 0; index < times.Length; index++)
        {
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"Point {index}", CreatedAt: times[index]));
        }

        var expected = (await connection.QueryAsync<Guid>("SELECT id FROM retro_point WHERE retrospective_id = @retro ORDER BY created_at, id", new { retro })).Select(id => id.ToString()).ToList();
        var key = await KeyAsync(connection, feature);
        var seen = new List<string>();
        var sizes = new List<int>();
        string? cursor = null;
        do
        {
            var result = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}","limit":2{{(cursor is null ? string.Empty : $$""","cursor":"{{cursor}}" """)}}}""");
            var ids = result.GetProperty("points").EnumerateArray().Select(point => point.GetProperty("id").GetString()!).ToList();
            seen.AddRange(ids);
            sizes.Add(ids.Count);
            result.GetProperty("total_points").GetInt32().ShouldBe(5);
            cursor = result.GetProperty("next_cursor").GetString();
        }
        while (cursor is not null);

        sizes.ShouldBe([2, 2, 1]);
        seen.ShouldBe(expected);
    }

    [Test]
    public async Task Export_ShouldCarryEachRetrosFailureLogOncePerPage()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: feature, AgentName: "agent-log"));
        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        for (var index = 0; index < 3; index++)
        {
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"Point {index}", CreatedAt: now.AddHours(-index - 1)));
        }

        await seed.FailureAsync(retro, RetroFailureKind.Blocker, "Blocked by the lock", 0);
        await seed.FailureAsync(retro, RetroFailureKind.AssumptionDisproved, "Assumed it was fast", 0);
        await seed.FailureAsync(retro, RetroFailureKind.Friction, "First friction", 0);
        await seed.FailureAsync(retro, RetroFailureKind.Friction, "Second friction", 1);
        var key = await KeyAsync(connection, feature);

        var first = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}","limit":2}""");
        var second = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}","limit":2,"cursor":"{{first.GetProperty("next_cursor").GetString()}}"}""");

        foreach (var page in new[] { first, second })
        {
            var entry = page.GetProperty("retros").EnumerateObject().Single();
            entry.Name.ShouldBe(retro.ToString());
            entry.Value.GetProperty("agent_name").GetString().ShouldBe("agent-log");
            entry.Value.GetProperty("role").GetString().ShouldBe("subAgent");
            var log = entry.Value.GetProperty("failure_log");
            Strings(log, "blockers").ShouldBe(["Blocked by the lock"]);
            Strings(log, "assumptions_disproved").ShouldBe(["Assumed it was fast"]);
            Strings(log, "friction").ShouldBe(["First friction", "Second friction"]);
        }
    }

    [Test]
    public async Task Export_WhenStoryHasTasksWithCommits_ShouldListTheirPathsCapped()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var story = await ItemAsync(connection, "story", feature);
        var task = await ItemAsync(connection, "task", story);
        var elsewhere = await ItemAsync(connection, "task");
        await PointOnAsync(connection, story, "On the story");
        var repository = await RepositoryAsync(connection);
        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        for (var index = 0; index < 22; index++)
        {
            await CommitAsync(connection, index < 5 ? story : task, repository, index, now.AddDays(-index), index == 0 ? 60 : 2);
        }

        await CommitAsync(connection, elsewhere, repository, 99, now, 1);

        var result = await ExportAsync($$"""{"project":"SHIP","feature":"{{await KeyAsync(connection, feature)}}"}""");
        var point = result.GetProperty("points").EnumerateArray().Single(candidate => candidate.GetProperty("text").GetString() == "On the story");
        var commits = point.GetProperty("commits").EnumerateArray().ToList();

        commits.Count.ShouldBe(20);
        commits[0].GetProperty("sha").GetString().ShouldBe(Sha(0));
        commits[0].GetProperty("paths").GetArrayLength().ShouldBe(50);
        commits[1].GetProperty("paths").GetArrayLength().ShouldBe(2);
        commits[0].GetProperty("repository").GetString().ShouldBe($"github.com/{repository.Path}");
        commits.Select(commit => commit.GetProperty("sha").GetString()).ShouldNotContain(Sha(99));
        commits.Select(commit => commit.GetProperty("sha").GetString()).ShouldNotContain(Sha(21));
    }

    [Test]
    public async Task Export_ShouldCutBriefAt2000Characters()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var story = await ItemAsync(connection, "story", feature);
        await connection.ExecuteAsync("UPDATE work_item SET description = @text WHERE id = @story", new { text = new string('a', 2500), story });
        await PointOnAsync(connection, story, "Long brief");
        var short1 = await ItemAsync(connection, "story", feature);
        await connection.ExecuteAsync("UPDATE work_item SET description = @text WHERE id = @short1", new { text = new string('b', 2000), short1 });
        await PointOnAsync(connection, short1, "Exact brief");

        var result = await ExportAsync($$"""{"project":"SHIP","feature":"{{await KeyAsync(connection, feature)}}"}""");

        var long1 = Brief(result, "Long brief");
        long1.Length.ShouldBe(2000);
        long1.ShouldEndWith("…");
        Brief(result, "Exact brief").ShouldBe(new string('b', 2000));
    }

    [Test]
    public async Task Export_WhenSprintRetro_ShouldHaveNullItemAndTheRetrosSprint()
    {
        await using var connection = await OpenDatabaseAsync();
        var name = $"Export sprint {Guid.NewGuid():N}";
        var sprint = await SprintAsync(connection, name);
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: sprint, AgentName: Agent()));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Sprint point", RootCause: null));

        var result = await ExportAsync($$"""{"project":"SHIP","sprint":"{{name}}"}""");

        var point = result.GetProperty("points").EnumerateArray().Single();
        point.GetProperty("item").ValueKind.ShouldBe(JsonValueKind.Null);
        point.GetProperty("feature").ValueKind.ShouldBe(JsonValueKind.Null);
        point.GetProperty("sprint").GetString().ShouldBe(name);
        point.GetProperty("role").GetString().ShouldBe("person");
        point.GetProperty("commits").GetArrayLength().ShouldBe(0);
        result.GetProperty("scope").GetProperty("sprint").GetString().ShouldBe(name);
        result.GetProperty("retros").EnumerateObject().Single().Value.GetProperty("item").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Export_WhenFeatureRetro_ShouldUseTheFeatureAsFeature()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var story = await ItemAsync(connection, "story", feature);
        await connection.ExecuteAsync("UPDATE work_item SET goal = 'The goal of it', description = 'Not this' WHERE id = @feature", new { feature });
        await PointOnAsync(connection, feature, "On the feature");
        await PointOnAsync(connection, story, "On the story");
        var key = await KeyAsync(connection, feature);

        var result = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}"}""");

        var own = result.GetProperty("points").EnumerateArray().Single(point => point.GetProperty("text").GetString() == "On the feature");
        own.GetProperty("item").GetProperty("key").GetString().ShouldBe(key);
        own.GetProperty("item").GetProperty("type").GetString().ShouldBe("feature");
        own.GetProperty("item").GetProperty("brief").GetString().ShouldBe("The goal of it");
        own.GetProperty("feature").GetProperty("key").GetString().ShouldBe(key);
        own.GetProperty("feature").GetProperty("goal").GetString().ShouldBe("The goal of it");
        var child = result.GetProperty("points").EnumerateArray().Single(point => point.GetProperty("text").GetString() == "On the story");
        child.GetProperty("feature").GetProperty("key").GetString().ShouldBe(key);
        child.GetProperty("item").GetProperty("type").GetString().ShouldBe("story");
    }

    [Test]
    public async Task Export_WhenExcludeCovered_ShouldLeaveOutEarlierNoiseFoldedAndOneOffButKeepUnclustered()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: feature, AgentName: Agent()));
        var points = new Dictionary<string, Guid>();
        foreach (var outcome in new[] { "noise", "folded", "oneOff", "unclustered", "clustered" })
        {
            points[outcome] = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"Was {outcome}"));
        }

        var fresh = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Never analysed"));
        var archivedOnly = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Only in an archived analysis"));
        await AnalysisAsync(connection, Project, archived: false, points.Select(pair => (pair.Value, pair.Key)).ToArray());
        await AnalysisAsync(connection, Project, archived: true, (archivedOnly, "noise"));
        var key = await KeyAsync(connection, feature);

        var excluded = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}"}""");
        var all = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}","exclude_covered":false}""");

        Texts(excluded).ShouldBe(["Was unclustered", "Was clustered", "Never analysed", "Only in an archived analysis"], ignoreOrder: true);
        excluded.GetProperty("total_points").GetInt32().ShouldBe(4);
        Texts(all).Count.ShouldBe(7);
        fresh.ShouldNotBe(Guid.Empty);
    }

    [Test]
    public async Task Export_FirstPageOnly_ShouldCarryOpenActionsAndPreviousPatterns()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: feature, AgentName: Agent()));
        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        var one = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "One", CreatedAt: now.AddHours(-3)));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Two", CreatedAt: now.AddHours(-2)));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Three", CreatedAt: now.AddHours(-1)));
        var theme = await seed.ThemeAsync($"Theme {Guid.NewGuid():N}");
        var action = await ItemAsync(connection, "action");
        await seed.ActionAsync(new RetroSeedAction(action, ThemeId: theme, FixType: RetroFixType.Analyzer, PointIds: [await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Actioned one")), await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Actioned two"))]));
        var analysis = await AnalysisAsync(connection, Project, archived: false, now.AddDays(1), (one, "clustered"));
        var pattern = await PatternAsync(connection, analysis);
        var key = await KeyAsync(connection, feature);
        var actionKey = await KeyAsync(connection, action);

        var first = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}","limit":2,"with_patterns_from":"{{analysis}}"}""");
        var latest = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}","limit":2,"with_patterns_from":"latest"}""");
        var second = await ExportAsync($$"""{"project":"SHIP","feature":"{{key}}","limit":2,"with_patterns_from":"{{analysis}}","cursor":"{{first.GetProperty("next_cursor").GetString()}}"}""");

        var open = first.GetProperty("open_actions").EnumerateArray().Single(candidate => candidate.GetProperty("key").GetString() == actionKey);
        open.GetProperty("fix_type").GetString().ShouldBe("analyzer");
        open.GetProperty("theme").GetString().ShouldNotBeNullOrEmpty();
        open.GetProperty("point_count").GetInt32().ShouldBe(2);
        var patterns = first.GetProperty("previous_patterns").EnumerateArray().ToList();
        patterns.Count.ShouldBe(1);
        patterns[0].GetProperty("id").GetString().ShouldBe(pattern.ToString());
        patterns[0].GetProperty("kind").GetString().ShouldBe("problem");
        patterns[0].GetProperty("embedding_model").GetString().ShouldBe("e5-small");
        patterns[0].GetProperty("centroid").EnumerateArray().Select(value => value.GetSingle()).ShouldBe([0.5f, 0.25f]);
        latest.GetProperty("previous_patterns").EnumerateArray().Single().GetProperty("id").GetString().ShouldBe(pattern.ToString());
        second.GetProperty("open_actions").GetArrayLength().ShouldBe(0);
        second.GetProperty("previous_patterns").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task Export_WhenPatternsFromAnotherProjectsAnalysis_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature");
        var foreign = await AnalysisAsync(connection, SeedIds.Projects.Pay, archived: false);

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "export_retro_points", $$"""{"project":"SHIP","feature":"{{await KeyAsync(connection, feature)}}","with_patterns_from":"{{foreign}}"}""");

        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("not_found");
    }

    private async Task<JsonElement> ExportAsync(string arguments)
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "export_retro_points", arguments);
        reply.IsToolError().ShouldBeFalse(reply.Body);
        return reply.Structured();
    }

    private static List<string> Texts(JsonElement result) => [.. result.GetProperty("points").EnumerateArray().Select(point => point.GetProperty("text").GetString()!)];

    private static List<string> Strings(JsonElement element, string name) => [.. element.GetProperty(name).EnumerateArray().Select(value => value.GetString()!)];

    private static string Brief(JsonElement result, string text)
        => result.GetProperty("points").EnumerateArray().Single(point => point.GetProperty("text").GetString() == text).GetProperty("item").GetProperty("brief").GetString()!;

    private static string Agent() => $"agent-{Guid.NewGuid():N}";

    private static string Sha(int index) => index.ToString("x", System.Globalization.CultureInfo.InvariantCulture).PadLeft(40, '0');

    private static async Task<Guid> ItemAsync(NpgsqlConnection connection, string type, Guid? parent = null)
    {
        var builder = new WorkItemBuilder(Project).WithType(type);
        return await (parent is { } id ? builder.WithParent(id) : builder).InsertAsync(connection);
    }

    private static Task<string> KeyAsync(NpgsqlConnection connection, Guid item) => connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @item", new { item });

    private static async Task<Guid> PointOnAsync(NpgsqlConnection connection, Guid item, string text)
    {
        var seed = new RetroSeed(connection, Project);
        return await seed.PointAsync(await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: item, AgentName: Agent())), new RetroSeedPoint(RetroCategory.WentBadly, text));
    }

    private static async Task<Guid> SprintAsync(NpgsqlConnection connection, string name)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @Project, @name, DATE '2001-01-01', DATE '2001-01-14')", new { id, Project, name });
        return id;
    }

    private static async Task<RepositoryRow> RepositoryAsync(NpgsqlConnection connection)
    {
        var row = new RepositoryRow(Guid.NewGuid(), $"acme/{Guid.NewGuid():N}");
        await connection.ExecuteAsync("INSERT INTO code_repository (id, provider, host, path) VALUES (@Id, 'github', 'github.com', @Path)", row);
        return row;
    }

    private static async Task CommitAsync(NpgsqlConnection connection, Guid item, RepositoryRow repository, int index, DateTimeOffset authoredAt, int paths)
    {
        var sha = Sha(index);
        await connection.ExecuteAsync(
            """
            INSERT INTO code_commit (repository_id, sha, parent_shas, subject, author_name, authored_at, additions, deletions, files_changed, paths, read_at)
            VALUES (@repository, @sha, '{}', @subject, 'Author', @authoredAt, 1, 1, @paths, @pathList, now())
            """,
            new { repository = repository.Id, sha, subject = $"Commit {index}", authoredAt, paths, pathList = Enumerable.Range(0, paths).Select(path => $"src/file{path}.cs").ToArray() });
        await connection.ExecuteAsync(
            "INSERT INTO work_item_commit (id, work_item_id, repository_id, sha, position, added_via) VALUES (@id, @item, @repository, @sha, @index, 'form')",
            new { id = Guid.NewGuid(), item, repository = repository.Id, sha, index = index + 1 });
    }

    private static Task<Guid> AnalysisAsync(NpgsqlConnection connection, Guid project, bool archived, params (Guid Point, string Outcome)[] points)
        => AnalysisAsync(connection, project, archived, null, points);

    private static async Task<Guid> AnalysisAsync(NpgsqlConnection connection, Guid project, bool archived, DateTimeOffset? createdAt, params (Guid Point, string Outcome)[] points)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO retro_analysis (id, project_id, scope_kind, range_from, range_to, created_by_user_id, agent_name, embedding_model, report_markdown, created_at, archived_at)
            VALUES (@id, @project, 'range', DATE '2001-01-01', DATE '2001-01-31', @user, 'agent-export', 'e5-small', '# Report', coalesce(@createdAt::timestamptz, now()), CASE WHEN @archived THEN now() END)
            """,
            new { id, project, user = SeedIds.Users.Joel, archived, createdAt });
        foreach (var (point, outcome) in points)
        {
            await connection.ExecuteAsync("INSERT INTO retro_analysis_point (analysis_id, point_id, outcome) VALUES (@id, @point, @outcome)", new { id, point, outcome });
        }

        return id;
    }

    private static async Task<Guid> PatternAsync(NpgsqlConnection connection, Guid analysis)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO retro_pattern (id, analysis_id, ref, kind, title, lesson, centroid, rank) VALUES (@id, @analysis, 'p1', 'problem', 'Checks run late', 'Run the check first.', @centroid, 1)",
            new { id, analysis, centroid = new[] { 0.5f, 0.25f } });
        return id;
    }

    private sealed record RepositoryRow(Guid Id, string Path);
}
