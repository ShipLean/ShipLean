using System.Net;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Plan;
using ShipLean.WebApi.Features.Mcp.Tools.Sprint;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.WorkItems.Bau;

// K2: a bau item never appears in a list or search result; it is reached only by its key, its id or a link to it.
// One host. The bau item has a unique title, a comment and a Markdown attachment; a control story and feature carry the same
// words, so a surface that returns nothing at all cannot pass. A bau item has no sprint and no parent (ck_work_item_bau_alone),
// so the sprint-scoped rows (taskboard, sprint tools, find_work_to_pick_up, Retro views) cannot see it through a sprint and are
// passing rows from the start: they pin that a new join cannot start to.
[TestFixture]
public class BauHiddenSweepTests : McpFixture
{
    private const string TitleWord = "zorblaxnik";
    private const string CommentWord = "quibblesnort";
    private const string AttachmentWord = "frumpelwick";

    // Seeded Sprint 24 content the sprint-scoped rows must still show: a story in the sprint, and a task that is ready to pick up.
    private const string SeededSprintStory = "SHIP-141";
    private const string SeededReadyTask = "SHIP-173";
    private const string ControlTheme = "Control theme";

    private Guid _bauId;
    private string _bauKey = string.Empty;
    private int _bauNumber;
    private string _controlFeatureKey = string.Empty;
    private string _controlStoryKey = string.Empty;
    private string _token = string.Empty;

    protected override bool WithProbeTools => false;

    private ApiClient Joel => Api.ActingAs(TestUser.Joel);

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        Host.ActAs(TestUser.Joel.Id);
        _token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        using (var scope = Host.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IWorkItemStore>();
            var bau = await store.CreateAsync(new WorkItemCreate(SeedIds.Projects.Ship, WorkItemTypeId.Bau, $"Rota {TitleWord} bau", null, null, null), WriteOptions.FromWebhook("ShipLean sprint demo"));
            _bauId = bau.Written!.Id;
            _bauKey = bau.Written.Key;
            var feature = await store.CreateAsync(new WorkItemCreate(SeedIds.Projects.Ship, WorkItemTypeId.Feature, $"Control {TitleWord} feature", null, null, null), WriteOptions.User);
            _controlFeatureKey = feature.Written!.Key;
            var story = await store.CreateAsync(new WorkItemCreate(SeedIds.Projects.Ship, WorkItemTypeId.Story, $"Control {TitleWord} story", feature.Written.Id, null, null), WriteOptions.User);
            _controlStoryKey = story.Written!.Key;
        }

        await using var connection = await OpenDatabaseAsync();
        _bauNumber = await connection.QuerySingleAsync<int>("SELECT number FROM work_item WHERE id = @id", new { id = _bauId });
        // The control is found through the same word in every leg, so each leg is proven able to find something.
        foreach (var key in new[] { _bauKey, _controlStoryKey })
        {
            (await CallToolAsync(_token, "comment", $$"""{"item":"{{key}}","text":"A note about {{CommentWord}} here."}""")).IsToolError().ShouldBeFalse();
            (await CallToolAsync(_token, "attach_markdown", $$"""{"feature":"{{key}}","name":"notes.md","content":"The {{AttachmentWord}} handshake."}""")).IsToolError().ShouldBeFalse();
        }

        // A retro point on the control story, so the Retro views have something to show.
        (await CallToolAsync(_token, "log_retrospective", $$"""{"item":"{{_controlStoryKey}}","went_well":[{"text":"The {{TitleWord}} retro point.","theme":"{{ControlTheme}}"}]}""")).IsToolError().ShouldBeFalse();

        using var reindexed = await Joel.PostJsonAsync("api/admin/search/reindex?full=true");
        reindexed.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task Search_WhenWordKeyNumberCommentOrAttachment_ShouldNotFindBau()
    {
        var bodies = new List<string>
        {
            await SearchAsync(TitleWord, _controlStoryKey),
            await SearchAsync(CommentWord, _controlStoryKey),
            await SearchAsync(AttachmentWord, _controlStoryKey),
            await SearchAsync(_bauKey, null),
            await SearchAsync(_bauNumber.ToString(), null, prefix: true),
        };

        bodies.ShouldAllBe(body => !LeaksBau(body));
    }

    [Test]
    public async Task WorkItemList_ShouldNotListBau()
    {
        var all = await GetAsync($"api/projects/{SeedIds.Projects.Ship}/work-items");
        AssertHidden(all);
        all.ShouldContain(_controlStoryKey);
        AssertHidden(await GetAsync($"api/projects/{SeedIds.Projects.Ship}/work-items?type=bau"));
    }

    [Test]
    public async Task Backlogs_ShouldNotListBau()
    {
        var project = SeedIds.Projects.Ship;
        foreach (var mode in new[] { "list", "tree", "planning" })
        {
            var body = await GetAsync($"api/projects/{project}/backlog?mode={mode}&includeCompleted=true");
            AssertHidden(body);
            body.ShouldContain(_controlStoryKey, Case.Sensitive, mode);
        }
        AssertHidden(await GetAsync($"api/projects/{project}/backlogs/features?includeCompleted=true"));
        AssertHidden(await GetAsync($"api/projects/{project}/backlogs/epics?includeCompleted=true"));
        (await GetAsync($"api/projects/{project}/backlogs/features?includeCompleted=true")).ShouldContain(_controlFeatureKey);
    }

    [Test]
    public async Task Boards_ShouldNotListBau()
    {
        await using var connection = await OpenDatabaseAsync();
        var epicKeys = (await connection.QueryAsync<string>(
            "SELECT p.key || '-' || w.number FROM work_item w JOIN project p ON p.id = w.project_id JOIN state s ON s.id = w.state_id WHERE w.project_id = @id AND w.type = 'epic' AND s.category NOT IN ('removed', 'completed')", new { id = SeedIds.Projects.Ship })).ToList();
        epicKeys.ShouldNotBeEmpty();

        // Each board must show a known card of its own level, so an empty board cannot pass.
        var boards = new Dictionary<Guid, string[]>
        {
            [SeedIds.Boards.ShipStories] = [_controlStoryKey],
            [SeedIds.Boards.ShipFeatures] = [_controlFeatureKey],
            [SeedIds.Boards.ShipEpics] = [.. epicKeys],
        };
        foreach (var (board, cards) in boards)
        {
            var body = await GetAsync($"api/boards/{board}");
            AssertHidden(body);
            cards.ShouldAllBe(key => body.Contains($"\"key\":\"{key}\"", StringComparison.Ordinal), $"board {board}");
        }
    }

    [Test]
    public async Task SprintViews_ShouldNotListBau()
    {
        var taskboard = await GetAsync($"api/sprints/{SeedIds.Sprints.Sprint24}/taskboard");
        var metrics = await GetAsync($"api/sprints/{SeedIds.Sprints.Sprint24}/metrics");
        var capacity = await GetAsync($"api/sprints/{SeedIds.Sprints.Sprint24}/capacity");

        // Known Sprint 24 content in each response, so an empty view cannot pass.
        taskboard.ShouldContain($"\"key\":\"{SeededSprintStory}\"");
        taskboard.ShouldContain($"\"key\":\"{SeededReadyTask}\"");
        var metricsJson = System.Text.Json.JsonDocument.Parse(metrics).RootElement;
        metricsJson.GetProperty("committed").GetDecimal().ShouldBeGreaterThan(0);
        metricsJson.GetProperty("burndown").GetArrayLength().ShouldBeGreaterThan(0);
        System.Text.Json.JsonDocument.Parse(capacity).RootElement.GetProperty("members").GetArrayLength().ShouldBeGreaterThan(0);

        new[] { taskboard, metrics, capacity }.ShouldAllBe(body => !LeaksBau(body));
    }

    [Test]
    public async Task McpSearch_WithAndWithoutWords_ShouldNotFindBau()
    {
        var words = await McpAsync("search", $$"""{"words":"{{TitleWord}}"}""");
        AssertHidden(words);
        words.ShouldContain(_controlStoryKey);
        AssertHidden(await McpAsync("search", $$"""{"words":"{{CommentWord}}","project":"SHIP"}"""));
        AssertHidden(await McpAsync("search", $$"""{"words":"{{AttachmentWord}}","project":"SHIP"}"""));
        var byState = await McpAsync("search", """{"project":"SHIP","state":"New","limit":25}""");
        AssertHidden(byState);
        byState.ShouldContain(_controlStoryKey);
    }

    [Test]
    public async Task WhatsChanged_ShouldNotListBauRevisionsOrComments()
    {
        var body = await McpAsync("whats_changed_since", $$"""{"project":"SHIP","since":"2000-01-01T00:00:00Z","mine":false,"limit":500}""");
        AssertHidden(body);
        body.ShouldContain(_controlStoryKey);
        body.ShouldContain(CommentWord);
    }

    [Test]
    public async Task McpBacklogAndSprintTools_ShouldNotListBau()
    {
        // Each tool with a control item it must list: the control story or feature, or seeded Sprint 24 content.
        var calls = new (string Tool, string Arguments, string Control)[]
        {
            ("review_backlog", """{"project":"SHIP","level":"stories","top":100}""", _controlStoryKey),
            ("review_backlog", """{"project":"SHIP","level":"features","top":100}""", _controlFeatureKey),
            ("find_work_to_pick_up", """{"project":"SHIP"}""", SeededReadyTask),
            ("plan_sprint", """{"project":"SHIP","sprint":"next","points_limit":1000}""", _controlStoryKey),
            ("end_sprint", """{"project":"SHIP","end_today":true}""", SeededSprintStory),
            ("sprint_status", """{"project":"SHIP"}""", SeededSprintStory),
            ("sprint_review", """{"project":"SHIP"}""", SeededSprintStory),
            ("feature_progress", $$"""{"item":"{{_controlFeatureKey}}"}""", _controlStoryKey),
        };

        foreach (var (tool, arguments, control) in calls)
        {
            var body = await McpAsync(tool, arguments);
            body.ShouldContain($"\"key\":\"{control}\"", Case.Sensitive, $"{tool} {arguments}");
            AssertHidden(body);
        }
    }

    [Test]
    public async Task PlanningAndRetroViews_ShouldNotListBau()
    {
        var project = SeedIds.Projects.Ship;
        using var duplicates = await Joel.PostJsonAsync($"api/projects/{project}/backlog/duplicates", new { titles = new[] { $"Rota {TitleWord} bau", $"Control {TitleWord} story" } });
        duplicates.StatusCode.ShouldBe(HttpStatusCode.OK);
        var duplicateText = await duplicates.Content.ReadAsStringAsync();
        AssertHidden(duplicateText);
        duplicateText.ShouldContain(_controlStoryKey);

        // The retro point on the control story is in the search rows, the insights and the theme list; Sprint 22's coverage lists its seeded stories.
        var retros = await GetAsync($"api/projects/{project}/retros?q={TitleWord}");
        AssertHidden(retros);
        retros.ShouldContain(_controlStoryKey);
        var insights = await GetAsync($"api/projects/{project}/retros/insights");
        AssertHidden(insights);
        insights.ShouldContain(ControlTheme);
        var themes = await GetAsync($"api/projects/{project}/retros/themes");
        AssertHidden(themes);
        themes.ShouldContain(ControlTheme);
        var sprintRetro = await GetAsync($"api/sprints/{SeedIds.Sprints.Sprint22}/retro");
        AssertHidden(sprintRetro);
        sprintRetro.ShouldContain("\"key\":\"SHIP-46\"");
        // No action item exists to offer, so this one is a shape check only: a bau item could not be offered but nothing would show it.
        AssertHidden(await GetAsync($"api/projects/{project}/retros/action-dialog"));
    }

    [Test]
    public async Task InstanceProjects_ShouldNotCountBau()
    {
        await using var connection = await OpenDatabaseAsync();
        var expected = await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.project_id = @id AND s.category <> 'removed' AND w.type <> 'bau'", new { id = SeedIds.Projects.Ship });
        var bauCount = await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id AND type = 'bau'", new { id = SeedIds.Projects.Ship });
        bauCount.ShouldBeGreaterThan(0);
        expected.ShouldBeGreaterThan(0);

        var projects = System.Text.Json.JsonDocument.Parse(await GetAsync("api/instance/projects")).RootElement;
        var ship = projects.EnumerateArray().Single(p => p.GetProperty("key").GetString() == "SHIP");

        ship.GetProperty("itemCount").GetInt32().ShouldBe(expected);
    }

    [Test]
    public async Task GetWorkItem_WhenByKey_ShouldReturnBau()
    {
        var mcp = await McpAsync("get_work_item", $$"""{"key":"{{_bauKey}}"}""");
        mcp.ShouldContain(_bauKey);
        mcp.ShouldContain(TitleWord);

        (await GetAsync($"api/work-items/{_bauId}")).ShouldContain(TitleWord);
        (await GetAsync($"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{_bauKey}")).ShouldContain(TitleWord);
    }

    [Test]
    public async Task Attachments_WhenListedOnBau_ShouldWork()
    {
        var listed = await GetAsync($"api/work-items/{_bauId}/attachments");

        listed.ShouldContain("notes.md");
    }

    [Test]
    public async Task MoveToSprint_WhenBau_ShouldBeValidation()
    {
        var reply = await CallToolAsync(_token, "move_to_sprint", $$"""{"items_to_move":[{"item":"{{_bauKey}}"}],"sprint":"current"}""");

        reply.IsToolError().ShouldBeTrue(reply.Body);
        reply.Body.ShouldContain(ApiErrorCodes.Validation);
        reply.ResultText().ShouldBe("A BAU item can't be in a sprint.");
        await AssertBauAloneAsync();
    }

    [Test]
    public async Task PlanSprint_WhenBauKeyInPlan_ShouldBeValidation()
    {
        Host.ActAs(TestUser.Joel.Id);
        var revision = await RevisionAsync();
        var sprint = SeedIds.Sprints.Sprint25;
        string sealedPlan;
        using (var scope = Host.Services.CreateScope())
        {
            sealedPlan = scope.ServiceProvider.GetRequiredService<PlanEnvelope>().Seal("plan_sprint", SeedIds.Projects.Ship, new PlanSprintPlan(sprint, [new PlanItem(_bauId, revision)]));
        }

        var reply = await CallToolAsync(_token, "plan_sprint", $$"""{"project":"SHIP","plan":"{{sealedPlan}}"}""");

        reply.IsToolError().ShouldBeTrue(reply.Body);
        reply.Body.ShouldContain(ApiErrorCodes.Validation);
        await AssertBauAloneAsync();
    }

    [Test]
    public async Task Patch_WhenBauGetsAParent_ShouldBeValidation()
    {
        using var response = await Joel.PatchJsonAsync($"api/work-items/{_bauId}", new { expectedRevision = await RevisionAsync(), changes = new { parent = new { value = SeedIds.WorkItems.Ship101 } } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        await AssertBauAloneAsync();
    }

    [Test]
    public async Task BreakDown_WhenBauParent_ShouldBeValidation()
    {
        var reply = await CallToolAsync(_token, "break_down", $$"""{"parent":"{{_bauKey}}","children":[{"title":"A child"}]}""");

        reply.IsToolError().ShouldBeTrue(reply.Body);
        reply.Body.ShouldContain(ApiErrorCodes.Validation);
    }

    // The items of a search, as their keys: the body also echoes the query, which may be the bau key itself.
    private async Task<string> SearchAsync(string query, string? control, bool prefix = false)
    {
        var body = await GetAsync($"api/search?q={Uri.EscapeDataString(query)}&closed=true&limit=25{(prefix ? "&prefix=true" : string.Empty)}");
        var items = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("items").GetRawText();
        if (control is not null)
        {
            items.ShouldContain(control, Case.Sensitive, query);
        }
        return items;
    }

    private async Task<string> GetAsync(string url)
    {
        using var response = await Joel.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{url}: {text}");
        return text;
    }

    private async Task<string> McpAsync(string tool, string arguments)
    {
        var reply = await CallToolAsync(_token, tool, arguments);
        reply.IsToolError().ShouldBeFalse($"{tool} {arguments}: {reply.Body}");
        return reply.Body;
    }

    private void AssertHidden(string body) => LeaksBau(body).ShouldBeFalse($"{_bauKey} is in: {body}");

    private bool LeaksBau(string body)
        => body.Contains(TitleWord + " bau", StringComparison.OrdinalIgnoreCase)
            || body.Contains(_bauId.ToString(), StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(body, $@"\b{Regex.Escape(_bauKey)}\b");

    private async Task<int> RevisionAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT revision FROM work_item WHERE id = @id", new { id = _bauId });
    }

    private async Task AssertBauAloneAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<(Guid? Sprint, Guid? Parent)>("SELECT sprint_id, parent_id FROM work_item WHERE id = @id", new { id = _bauId })).ShouldBe((null, null));
    }
}
