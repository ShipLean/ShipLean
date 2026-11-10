using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Asks;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Orient;

// get_work_item, search and list_projects over the seeded SHIP project, called through /mcp as Joel with a read token.
// Comments come from a pinned double (ListedCommentStore). The items are arranged once: a feature with two stories (one Priya's and active, one Joel's and new), two tasks under the
// first story, comments on it, and an agent's second revision.
[TestFixture]
public class OrientItemTests : McpFixture
{
    private const string AgentName = "Claude Code (unverified)";

    private Guid _feature;
    private Guid _story;
    private Guid _otherStory;
    private string _featureKey = string.Empty;
    private string _storyKey = string.Empty;
    private string _otherStoryKey = string.Empty;
    private string _descriptionOnlyKey = string.Empty;
    private string _partKey = string.Empty;
    private string[] _taskKeys = [];

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddSingleton<ICommentStore, ListedCommentStore>();
    };

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        _feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Zephyr settlement rework").InsertAsync(connection);
        _story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Zephyr gateway timeout on settle").WithParent(_feature)
            .WithStateNamed("Active").WithAssignee(SeedIds.Users.Priya).WithSprint(SeedIds.Sprints.Sprint24).WithTags("payments").WithPoints(3).InsertAsync(connection);
        _otherStory = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Zephyr settle page layout").WithParent(_feature)
            .WithAssignee(SeedIds.Users.Joel).InsertAsync(connection);
        var descriptionOnly = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Receipt email wording").InsertAsync(connection);
        var taskA = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("Add retry to the call").WithParent(_story).InsertAsync(connection);
        var taskB = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("Write the timeout test").WithParent(_story).WithAssignee(SeedIds.Users.Priya).InsertAsync(connection);
        var part = await new WorkItemBuilder(SeedIds.Projects.Part).InsertAsync(connection);
        for (var i = 0; i < 26; i++)
        {
            await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle($"Quokka chore {i}").InsertAsync(connection);
        }

        // The builders know neither a description nor an agent's name, so these two are set directly.
        await connection.ExecuteAsync("UPDATE work_item SET description = 'The mail mentions zephyr gateway fees.' WHERE id = @descriptionOnly", new { descriptionOnly });
        await new RevisionBuilder(_story).WithRev(2).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges("""[{"field":"points","from":null,"to":3,"fromLabel":null,"toLabel":null}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET agent_name = @AgentName WHERE work_item_id = @_story AND rev = 2", new { AgentName, _story });
        await connection.ExecuteAsync("UPDATE work_item SET revision = 2 WHERE id = @_story", new { _story });

        _featureKey = await KeyAsync(connection, _feature);
        _storyKey = await KeyAsync(connection, _story);
        _otherStoryKey = await KeyAsync(connection, _otherStory);
        _descriptionOnlyKey = await KeyAsync(connection, descriptionOnly);
        _partKey = await KeyAsync(connection, part);
        _taskKeys = [await KeyAsync(connection, taskA), await KeyAsync(connection, taskB)];

        var comments = Host.Services.GetRequiredService<ICommentStore>();
        for (var i = 1; i <= 6; i++)
        {
            await comments.AddAsync(_story, new CommentCreate($"Comment {i}"));
        }

        // The word searches below read search_doc. A full pass indexes the items above (real work_item rows, so the full sync keeps them).
        using var reindexed = await Api.ActingAs(TestUser.Joel).PostJsonAsync("api/admin/search/reindex?full=true");
        reindexed.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task GetWorkItem_WhenClientReadsOnlyText_ShouldCarryTheWholeItemAsJson()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{_storyKey}}"}""");

        var item = reply.TextJson();
        JsonElement.DeepEquals(item, reply.Structured()).ShouldBeTrue();
        item.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ShouldBe(["payments"]);
        item.GetProperty("children").GetArrayLength().ShouldBe(2);
    }

    [Test]
    public async Task GetWorkItem_WhenItemHasDecisionsAndQuestions_ShouldReturnThem()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", """{"key":"SHIP-124"}""");

        var item = reply.Structured();
        item.GetProperty("open_decisions").GetInt32().ShouldBe(4);
        item.GetProperty("open_questions").GetInt32().ShouldBe(2);
        var decisions = item.GetProperty("decisions").EnumerateArray().ToList();
        decisions.Select(d => d.GetProperty("number").GetInt32()).ShouldBe([1, 2, 3, 4]);
        decisions.ShouldAllBe(d => d.GetProperty("state").GetString() == "open" && d.GetProperty("answer_kind").GetString() == "recommended");
        decisions.Select(d => d.GetProperty("label").GetString()).ShouldBe(["D1", "D2", "D3", "D4"]);
        decisions[0].GetProperty("question").GetString().ShouldBe("Where does the Apple Pay button sit on the payment step?");
        decisions[0].GetProperty("current_answer").GetString().ShouldBe("Above the card form");
        decisions[0].GetProperty("alternatives").EnumerateArray().Select(a => a.GetString()).ShouldBe(["Below the card form", "In the order summary"]);
        decisions[0].GetProperty("agent_name").GetString().ShouldBe("Claude Code");
        decisions[0].GetProperty("history_count").GetInt32().ShouldBe(1);
        var questions = item.GetProperty("questions").EnumerateArray().ToList();
        questions.Count.ShouldBe(2);
        questions.Select(q => q.GetProperty("label").GetString()).ShouldBe(["Q1", "Q2"]);
        questions[0].GetProperty("asked_by").GetString().ShouldBe("Tom Okafor");
        questions[0].GetProperty("agent_name").GetString().ShouldBe("Claude Code");
        questions[0].GetProperty("state").GetString().ShouldBe("open");
        reply.Texts()[0].ShouldEndWith(" · 2 open questions · 4 decisions going with recommended.");
    }

    [Test]
    public async Task GetWorkItem_WhenQuestionAnsweredOrWithdrawn_ShouldListBothWithTheAnswerBody()
    {
        Guid item;
        string key;
        await using (var connection = await OpenDatabaseAsync())
        {
            item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Asks arrangement").InsertAsync(connection);
            key = await KeyAsync(connection, item);
            var comment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Joel).WithBody("Use **merchant.prod**").InsertAsync(connection);
            await new QuestionBuilder(item).Number(1).Text("Which merchant account?").Answered(comment, SeedIds.Users.Joel, AskDefaults.Now()).BuildAsync(connection);
            await new QuestionBuilder(item).Number(2).Text("Dropped question").Withdrawn(SeedIds.Users.Joel, AskDefaults.Now()).BuildAsync(connection);
            await new DecisionBuilder(item).Number(1).Question("Retries?").State("confirmed").AnsweredBy(SeedIds.Users.Joel, AskDefaults.Now(), "Claude Code (test)").BuildAsync(connection);
        }

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");

        var result = reply.Structured();
        var questions = result.GetProperty("questions").EnumerateArray().ToList();
        questions.Select(q => q.GetProperty("state").GetString()).ShouldBe(["answered", "withdrawn"]);
        questions[0].GetProperty("answer").GetProperty("body").GetString().ShouldBe("Use **merchant.prod**");
        questions[0].GetProperty("answer").GetProperty("by").GetString().ShouldBe("Joel Dickson");
        questions[1].GetProperty("withdrawn_at").GetString().ShouldNotBeNullOrEmpty();
        result.GetProperty("open_questions").GetInt32().ShouldBe(0);
        result.GetProperty("decisions")[0].GetProperty("state").GetString().ShouldBe("confirmed");
        result.GetProperty("decisions")[0].GetProperty("answered_agent_name").GetString().ShouldBe("Claude Code (test)");
        reply.Texts()[0].ShouldEndWith("recent comments.");
    }

    [Test]
    public async Task GetWorkItem_WhenNone_ShouldReturnEmptyListsAndNoSummarySuffix()
    {
        string key;
        await using (var connection = await OpenDatabaseAsync())
        {
            var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Bare story").InsertAsync(connection);
            key = await KeyAsync(connection, item);
        }

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");

        var result = reply.Structured();
        result.GetProperty("decisions").GetArrayLength().ShouldBe(0);
        result.GetProperty("questions").GetArrayLength().ShouldBe(0);
        result.GetProperty("open_decisions").GetInt32().ShouldBe(0);
        result.GetProperty("open_questions").GetInt32().ShouldBe(0);
        reply.Texts()[0].ShouldEndWith("recent comments.");
    }

    [Test]
    public async Task GetWorkItem_WhenOneOpenOfEach_ShouldUseSingularSuffix()
    {
        string key;
        await using (var connection = await OpenDatabaseAsync())
        {
            var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("One of each").InsertAsync(connection);
            key = await KeyAsync(connection, item);
            await new QuestionBuilder(item).Number(1).BuildAsync(connection);
            await new DecisionBuilder(item).Number(1).BuildAsync(connection);
        }

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");

        reply.Texts()[0].ShouldEndWith(" · 1 open question · 1 decision going with recommended.");
    }

    [Test]
    public async Task ListProjects_ShouldShowRoleAndCurrentSprint()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "list_projects");

        var projects = reply.Structured().GetProperty("projects").EnumerateArray().ToDictionary(p => p.GetProperty("key").GetString()!);
        projects["SHIP"].GetProperty("my_role").GetString().ShouldBe("admin");
        projects["SHIP"].GetProperty("current_sprint").GetProperty("name").GetString().ShouldBe("Sprint 24");
        projects["PAY"].TryGetProperty("current_sprint", out _).ShouldBeFalse();
        projects.ShouldNotContainKey("PART");
        reply.Structured().GetProperty("open_url").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task GetWorkItem_WhenFeature_ShouldIncludeGoalStatusAndVerified()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE work_item SET goal = 'Settle faster', status_line = 'On track', slug = 'zephyr-settlement', started_on = '2026-01-05', status_verified_at = now(), status_verified_by = @joel, status_verified_via = 'set_status' WHERE id = @_feature",
                new { joel = SeedIds.Users.Joel, _feature });
        }

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{_featureKey}}"}""");

        var item = reply.Structured();
        item.GetProperty("goal").GetString().ShouldBe("Settle faster");
        item.GetProperty("status").GetString().ShouldBe("On track");
        item.GetProperty("slug").GetString().ShouldBe("zephyr-settlement");
        item.GetProperty("started").GetString().ShouldNotBeNullOrEmpty();
        var verified = item.GetProperty("verified");
        verified.GetProperty("via").GetString().ShouldBe("set_status");
        verified.GetProperty("by").GetString().ShouldBe("Joel Dickson");
        verified.GetProperty("label").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task GetWorkItem_ShouldIncludeParentChildrenAndRevision()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{_storyKey.ToLowerInvariant()}}"}""");

        var item = reply.Structured();
        item.GetProperty("key").GetString().ShouldBe(_storyKey);
        item.GetProperty("project").GetString().ShouldBe("SHIP");
        item.GetProperty("open_url").GetString().ShouldEndWith($"/p/SHIP/items/{_storyKey}");
        item.GetProperty("revision").GetInt32().ShouldBe(2);
        item.GetProperty("state").GetString().ShouldBe("Active");
        item.GetProperty("assignee").GetString().ShouldBe("Priya Shah");
        item.GetProperty("sprint").GetString().ShouldBe("Sprint 24");
        item.GetProperty("points").GetDecimal().ShouldBe(3m);
        item.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ShouldBe(["payments"]);

        var parents = item.GetProperty("parents").EnumerateArray().ToList();
        parents.Count.ShouldBe(1);
        parents[0].GetProperty("key").GetString().ShouldBe(_featureKey);

        var children = item.GetProperty("children").EnumerateArray().ToList();
        children.Select(c => c.GetProperty("key").GetString()).ShouldBe(_taskKeys, ignoreOrder: true);
        children.Single(c => c.GetProperty("key").GetString() == _taskKeys[1]).GetProperty("assignee").GetString().ShouldBe("Priya Shah");
        children.ShouldAllBe(c => c.GetProperty("state").GetString() == "New");

        var comments = item.GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("body").GetString()).ToList();
        comments.ShouldBe(["Comment 6", "Comment 5", "Comment 4", "Comment 3", "Comment 2"]);
        item.GetProperty("comments_older").GetString().ShouldNotBeNullOrEmpty();

        var older = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item",
            $$"""{"key":"{{_storyKey}}","comments_before":"{{item.GetProperty("comments_older").GetString()}}"}""");
        older.Structured().GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("body").GetString()).ShouldBe(["Comment 1"]);

        var history = item.GetProperty("history").EnumerateArray().ToList();
        history.Select(h => h.GetProperty("revision").GetInt32()).ShouldBe([2, 1]);
        history[0].GetProperty("agent_name").GetString().ShouldBe(AgentName);
        history[0].GetProperty("who").GetString().ShouldNotBeNullOrEmpty();
        history[1].TryGetProperty("agent_name", out _).ShouldBeFalse();
    }

    [Test]
    public async Task GetWorkItem_WhenNotMember_ShouldBeNotFound()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{_partKey}}"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task GetWorkItem_WhenKeyUnknown_ShouldBeNotFound()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", """{"key":"SHIP-99999"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    // The words are ANDed in the lexical leg (an OR costs the hybrid ranking top-5 accuracy), so "zephyr gateway" needs both words in a row:
    // the story has both in its title, the receipt story only in its description. The layout story ("Zephyr settle page layout") misses a
    // word, so only the semantic leg can surface it; the test pins the two full matches coming first, in that order.
    [Test]
    public async Task Search_WhenWords_ShouldRankFullMatchesFirstInTheirOrder()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "search", """{"words":"zephyr gateway","project":"SHIP","type":"story"}""");

        Keys(reply).Take(2).ShouldBe([_storyKey, _descriptionOnlyKey]);
        reply.Structured().GetProperty("more").GetBoolean().ShouldBeFalse();
        reply.Structured().GetProperty("project").GetString().ShouldBe("SHIP");
    }

    [Test]
    public async Task Search_WhenStateAndAssignee_ShouldFilter()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var active = await CallToolAsync(token, "search", $$"""{"project":"SHIP","state":"active","parent":"{{_featureKey}}"}""");
        var priyas = await CallToolAsync(token, "search", $$"""{"project":"SHIP","assignee":"Priya","parent":"{{_featureKey}}"}""");
        var mine = await CallToolAsync(token, "search", $$"""{"project":"SHIP","assignee":"me","state":"New","type":"story","parent":"{{_featureKey}}"}""");
        var tagged = await CallToolAsync(token, "search", """{"project":"SHIP","tag":"Payments","words":"zephyr"}""");

        Keys(active).ShouldBe([_storyKey]);
        Keys(priyas).ShouldBe([_storyKey]);
        Keys(mine).ShouldBe([_otherStoryKey]);
        Keys(tagged).ShouldBe([_storyKey]);
    }

    [Test]
    public async Task Search_WhenMoreThanTwentyFiveMatch_ShouldCapAndSayMore()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "search", """{"project":"SHIP","words":"quokka","limit":25}""");

        reply.Structured().GetProperty("matches").GetArrayLength().ShouldBe(25);
        reply.Structured().GetProperty("more").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task Search_WhenNothingToSearchBy_ShouldBeValidation()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "search", """{"project":"SHIP"}""");

        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
    }

    private static string[] Keys(McpReply reply)
        => [.. reply.Structured().GetProperty("matches").EnumerateArray().Select(m => m.GetProperty("key").GetString()!)];

    private static Task<string> KeyAsync(Npgsql.NpgsqlConnection connection, Guid id)
        => connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
}
