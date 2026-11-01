using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Retros;

// log_retrospective over /mcp against the real log and the host's default (pick) themer. Every test arranges its own items in SHIP.
[TestFixture]
public class LogRetrospectiveTests : McpFixture
{
    private const string AgentName = "Test Agent (unverified)";

    private string _bearer = string.Empty;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.RemoveAll<IProjectEvents>();
        services.AddSingleton<RecordedEvents>();
        services.AddSingleton<IProjectEvents>(sp => sp.GetRequiredService<RecordedEvents>());
    };

    [OneTimeSetUp]
    public async Task IssueAToken() => _bearer = await TokenAsync(TestUser.Joel, ReadWrite);

    [Test]
    public async Task Log_WhenSubAgentOnStory_ShouldStorePointsLogAndAgentName()
    {
        var story = await ArrangeAsync(b => b.WithType("story"));
        var key = await KeyOfAsync(story);
        const string text = "Line one\nLine two  with  spaces, ünïcode ✓ and \"quotes\"";

        var result = await CallAsync("log_retrospective", Json(new
        {
            item = key,
            went_well = new[] { new { text } },
            went_badly = new[] { new { text = "The lock was held", theme = (string?)null, root_cause = "Builds queue behind one lock" } },
            failure_log = new { blockers = new[] { "Blocked 300 s\non the lock" }, assumptions_disproved = new[] { "CI matches local" }, friction = new[] { "Two retries" } },
        }));

        result.GetProperty("item").GetString().ShouldBe(key);
        result.GetProperty("role").GetString().ShouldBe("subAgent");
        result.GetProperty("points_added").GetInt32().ShouldBe(2);
        result.GetProperty("theme_mode").GetString().ShouldBe("pick");
        await using var connection = await OpenDatabaseAsync();
        var points = (await connection.QueryAsync<(string Category, string Text, string? RootCause, string? Agent)>(
            "SELECT p.category, p.text, p.root_cause, p.agent_name FROM retro_point p JOIN retrospective r ON r.id = p.retrospective_id WHERE r.work_item_id = @story ORDER BY p.position", new { story })).ToList();
        points.Select(p => p.Category).ShouldBe(["wentWell", "wentBadly"]);
        points[0].Text.ShouldBe(text);
        points[1].RootCause.ShouldBe("Builds queue behind one lock");
        points.ShouldAllBe(p => p.Agent == AgentName);
        var log = (await connection.QueryAsync<(string Kind, string Text)>(
            "SELECT l.kind, l.text FROM retro_failure_log l JOIN retrospective r ON r.id = l.retrospective_id WHERE r.work_item_id = @story ORDER BY l.position", new { story })).ToList();
        log.ShouldBe([("blocker", "Blocked 300 s\non the lock"), ("assumptionDisproved", "CI matches local"), ("friction", "Two retries")]);
    }

    [Test]
    public async Task Log_WhenSameAgentAgain_ShouldAppendNotDuplicate()
    {
        var story = await ArrangeAsync(b => b.WithType("story"));
        var key = await KeyOfAsync(story);

        var first = await CallAsync("log_retrospective", Json(new { item = key, went_well = new[] { new { text = "First" } }, partial = true }));
        var second = await CallAsync("log_retrospective", Json(new { item = key, improve = new[] { new { text = "Second" } }, failure_log = new { friction = new[] { "Late friction" } }, partial = false }));

        second.GetProperty("retro_id").GetGuid().ShouldBe(first.GetProperty("retro_id").GetGuid());
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retrospective WHERE work_item_id = @story", new { story })).ShouldBe(1);
        var texts = await connection.QueryAsync<string>("SELECT text FROM retro_point p JOIN retrospective r ON r.id = p.retrospective_id WHERE r.work_item_id = @story ORDER BY p.position", new { story });
        texts.ShouldBe(["First", "Second"]);
        (await connection.QuerySingleAsync<bool>("SELECT partial FROM retrospective WHERE work_item_id = @story", new { story })).ShouldBeFalse();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retro_failure_log l JOIN retrospective r ON r.id = l.retrospective_id WHERE r.work_item_id = @story", new { story })).ShouldBe(1);
    }

    [Test]
    public async Task Log_WhenFeatureWithCompiledFrom_ShouldBeConductorWithSources()
    {
        var feature = await ArrangeAsync(b => b.WithType("feature"));
        var storyA = await ArrangeAsync(b => b.WithType("story").WithParent(feature));
        var storyB = await ArrangeAsync(b => b.WithType("story").WithParent(feature));
        var keyA = await KeyOfAsync(storyA);
        var keyB = await KeyOfAsync(storyB);
        var sub = await TokenAsync(TestUser.Joel, ReadWrite, "Lane A");
        (await CallToolAsync(sub, "log_retrospective", Json(new { item = keyA, went_well = new[] { new { text = "Lane A went well" } } }))).IsToolError().ShouldBeFalse();

        var result = await CallAsync("log_retrospective", Json(new { item = await KeyOfAsync(feature), improve = new[] { new { text = "Compiled" } }, compiled_from = new[] { keyA, keyB } }));

        result.GetProperty("role").GetString().ShouldBe("conductor");
        result.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldBe([$"{keyB} has no retrospective yet, so it is not recorded as a source."]);
        await using var connection = await OpenDatabaseAsync();
        var sources = await connection.QueryAsync<Guid>(
            "SELECT src.work_item_id FROM retrospective_source s JOIN retrospective src ON src.id = s.source_retrospective_id WHERE s.retrospective_id = @id", new { id = result.GetProperty("retro_id").GetGuid() });
        sources.ShouldBe([storyA]);
    }

    [Test]
    public async Task Log_WhenFeatureWithoutCompiledFrom_ShouldStillBeConductor()
    {
        var feature = await ArrangeAsync(b => b.WithType("feature"));

        var result = await CallAsync("log_retrospective", Json(new { item = await KeyOfAsync(feature), went_well = new[] { new { text = "Feature level" } } }));

        result.GetProperty("role").GetString().ShouldBe("conductor");
    }

    [Test]
    public async Task Log_WhenThemeSupplied_ShouldUseOrCreateTheme()
    {
        var name = $"Theme {Guid.NewGuid():N}";
        var first = await ArrangeAsync(b => b.WithType("story"));
        var second = await ArrangeAsync(b => b.WithType("story"));

        var one = await CallAsync("log_retrospective", Json(new { item = await KeyOfAsync(first), improve = new[] { new { text = "One", theme = name } } }));
        await CallAsync("log_retrospective", Json(new { item = await KeyOfAsync(second), improve = new[] { new { text = "Two", theme = name.ToUpperInvariant() } } }));

        await using var connection = await OpenDatabaseAsync();
        var themes = (await connection.QueryAsync<(Guid Id, int Count)>(
            "SELECT id, point_count FROM retro_theme WHERE lower(name) = lower(@name) AND archived_at IS NULL", new { name })).ToList();
        themes.Count.ShouldBe(1);
        themes[0].Count.ShouldBe(2);
        one.GetProperty("themes").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ShouldContain(name);
    }

    [Test]
    public async Task Log_WhenNoTheme_ShouldBeUnthemed()
    {
        var story = await ArrangeAsync(b => b.WithType("story"));

        await CallAsync("log_retrospective", Json(new { item = await KeyOfAsync(story), improve = new[] { new { text = "No theme" } } }));

        await using var connection = await OpenDatabaseAsync();
        var themeId = await connection.QuerySingleAsync<Guid?>("SELECT p.theme_id FROM retro_point p JOIN retrospective r ON r.id = p.retrospective_id WHERE r.work_item_id = @story", new { story });
        themeId.ShouldBeNull();
    }

    [Test]
    public async Task Log_WhenRootCauseOnImprove_ShouldBeValidationError()
    {
        var story = await ArrangeAsync(b => b.WithType("story"));

        var reply = await CallToolAsync(_bearer, "log_retrospective", Json(new { item = await KeyOfAsync(story), improve = new[] { new { text = "Nope", root_cause = "Not allowed here" } } }));

        ErrorCode(reply).ShouldBe("validation");
        await AssertNoRetroAsync(story);
    }

    [Test]
    public async Task Log_WhenUnknownItem_ShouldBeNotFound()
    {
        var reply = await CallToolAsync(_bearer, "log_retrospective", Json(new { item = "SHIP-999999", went_well = new[] { new { text = "x" } } }));

        ErrorCode(reply).ShouldBe("not_found");
    }

    [Test]
    public async Task Log_WhenNoPointsAndNoLog_ShouldBeValidationError()
    {
        var story = await ArrangeAsync(b => b.WithType("story"));

        var reply = await CallToolAsync(_bearer, "log_retrospective", Json(new { item = await KeyOfAsync(story) }));

        ErrorCode(reply).ShouldBe("validation");
        await AssertNoRetroAsync(story);
    }

    [TestCase("PART-1")]
    [TestCase("SHIP-999999")]
    public async Task Log_WhenCompiledFromKeyNotInProject_ShouldBeValidationError(string foreignKey)
    {
        var feature = await ArrangeAsync(b => b.WithType("feature"));

        var reply = await CallToolAsync(_bearer, "log_retrospective", Json(new { item = await KeyOfAsync(feature), went_well = new[] { new { text = "x" } }, compiled_from = new[] { foreignKey } }));

        ErrorCode(reply).ShouldBe("validation");
        await AssertNoRetroAsync(feature);
    }

    [Test]
    public async Task Log_WhenTextOver2000Chars_ShouldBeValidationError()
    {
        var story = await ArrangeAsync(b => b.WithType("story"));

        var reply = await CallToolAsync(_bearer, "log_retrospective", Json(new { item = await KeyOfAsync(story), went_well = new[] { new { text = new string('a', 2001) } } }));
        var exact = await CallToolAsync(_bearer, "log_retrospective", Json(new { item = await KeyOfAsync(story), went_well = new[] { new { text = new string('a', 2000) } } }));

        ErrorCode(reply).ShouldBe("validation");
        exact.IsToolError().ShouldBeFalse(exact.Body);
    }

    [Test]
    public async Task Log_WhenStoryInSprint_ShouldAttributeToThatSprint()
    {
        var sprint = Guid.NewGuid();
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@sprint, @project, @name, DATE '2001-01-01', DATE '2001-01-14')",
                new { sprint, project = SeedIds.Projects.Ship, name = $"Retro Log {sprint:N}" });
        }

        var story = await ArrangeAsync(b => b.WithType("story").WithSprint(sprint));
        var unplanned = await ArrangeAsync(b => b.WithType("story"));

        await CallAsync("log_retrospective", Json(new { item = await KeyOfAsync(story), went_well = new[] { new { text = "In sprint" } } }));
        await CallAsync("log_retrospective", Json(new { item = await KeyOfAsync(unplanned), went_well = new[] { new { text = "No sprint" } } }));

        await using var check = await OpenDatabaseAsync();
        (await check.QuerySingleAsync<Guid?>("SELECT sprint_id FROM retrospective WHERE work_item_id = @story", new { story })).ShouldBe(sprint);
        (await check.QuerySingleAsync<Guid?>("SELECT sprint_id FROM retrospective WHERE work_item_id = @unplanned", new { unplanned })).ShouldBeNull();
    }

    [Test]
    public async Task Log_WhenItemIsTask_ShouldBeValidationError()
    {
        var story = await ArrangeAsync(b => b.WithType("story"));
        var task = await ArrangeAsync(b => b.WithType("task").WithParent(story));

        var reply = await CallToolAsync(_bearer, "log_retrospective", Json(new { item = await KeyOfAsync(task), went_well = new[] { new { text = "x" } } }));

        ErrorCode(reply).ShouldBe("validation");
        await AssertNoRetroAsync(task);
    }

    [Test]
    public async Task Log_WhenReadOnlyToken_ShouldBeRefused()
    {
        var story = await ArrangeAsync(b => b.WithType("story"));
        var readOnly = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(readOnly, "log_retrospective", Json(new { item = await KeyOfAsync(story), went_well = new[] { new { text = "x" } } }));

        reply.Status.ShouldBe(System.Net.HttpStatusCode.Forbidden);
        await AssertNoRetroAsync(story);
    }

    [Test]
    public async Task Log_WhenLogged_ShouldPublishRetroChanged()
    {
        var sprint = Guid.NewGuid();
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@sprint, @project, @name, DATE '2002-01-01', DATE '2002-01-14')",
                new { sprint, project = SeedIds.Projects.Ship, name = $"Retro Event {sprint:N}" });
        }

        var story = await ArrangeAsync(b => b.WithType("story").WithSprint(sprint));

        await CallAsync("log_retrospective", Json(new { item = await KeyOfAsync(story), went_well = new[] { new { text = "Event" } } }));

        var published = Host.Services.GetRequiredService<RecordedEvents>().Published.OfType<RetroChanged>().Where(e => e.WorkItemIds.Contains(story)).ToList();
        published.Count.ShouldBe(1);
        published[0].ProjectId.ShouldBe(SeedIds.Projects.Ship);
        published[0].SprintIds.ShouldBe([sprint]);
    }

    private async Task AssertNoRetroAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retrospective WHERE work_item_id = @item", new { item })).ShouldBe(0);
    }

    private static string Json(object value) => JsonSerializer.Serialize(value);

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

    private async Task<Guid> ArrangeAsync(Func<WorkItemBuilder, WorkItemBuilder> configure)
    {
        await using var connection = await OpenDatabaseAsync();
        return await configure(new WorkItemBuilder(SeedIds.Projects.Ship)).InsertAsync(connection);
    }

    private async Task<string> KeyOfAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
    }

    // The bus the host runs on in this class: it keeps what was published and lets subscribers subscribe to nothing.
    private sealed class RecordedEvents : IProjectEvents
    {
        private readonly List<ProjectEvent> _published = [];

        public IReadOnlyList<ProjectEvent> Published
        {
            get
            {
                lock (_published)
                {
                    return [.. _published];
                }
            }
        }

        public Task PublishAsync(ProjectEvent projectEvent)
        {
            lock (_published)
            {
                _published.Add(projectEvent);
            }

            return Task.CompletedTask;
        }

        public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => new Subscription();

        private sealed class Subscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
