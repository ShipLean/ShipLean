using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Search;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Orient.Search;

// What the search tool asks the ranker, and what it makes of the answer, against a recording ranker (no index involved).
[TestFixture]
public class SearchRoutingTests : McpFixture
{
    private readonly RecordingSearchRanker _ranker = new();

    private Guid _feature;
    private string _featureKey = string.Empty;
    private string _olderKey = string.Empty;
    private string _newerKey = string.Empty;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.Replace(ServiceDescriptor.Singleton<ISearchRanker>(_ranker));
    };

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        _feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Routing feature").InsertAsync(connection);
        var older = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("Routing older").WithTags("routing").InsertAsync(connection);
        var newer = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("Routing newer").WithTags("routing").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET changed_at = now() - interval '2 days' WHERE id = @older", new { older });
        await connection.ExecuteAsync("UPDATE work_item SET changed_at = now() - interval '1 day' WHERE id = @newer", new { newer });
        _featureKey = await KeyAsync(connection, _feature);
        _olderKey = await KeyAsync(connection, older);
        _newerKey = await KeyAsync(connection, newer);
    }

    [SetUp]
    public void ResetRanker() => _ranker.Results = new SearchResults(false, [], [], [], false);

    [Test]
    public async Task Search_WhenNoProject_ShouldSearchAllYourProjects()
    {
        var reply = await SearchAsync("""{"words":"checkout timeout"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var query = LastQuery;
        query.Text.ShouldBe("checkout timeout");
        query.ProjectIds.ShouldBeNull();
        query.Criteria.ShouldBeNull();
        query.Limit.ShouldBe(10);
        query.IncludeClosed.ShouldBeTrue();
        reply.Structured().TryGetProperty("project", out _).ShouldBeFalse();
    }

    [Test]
    public async Task Search_WhenProjectGiven_ShouldNarrow()
    {
        var reply = await SearchAsync("""{"words":"checkout","project":"SHIP","type":"bug","tag":"Payments","include_closed":false}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var query = LastQuery;
        query.ProjectIds.ShouldBe([SeedIds.Projects.Ship]);
        query.Types.ShouldBe([WorkItemTypeId.Bug]);
        query.Tags.ShouldBe(["Payments"]);
        query.IncludeClosed.ShouldBeFalse();
        reply.Structured().GetProperty("project").GetString().ShouldBe("SHIP");
    }

    [Test]
    public async Task Search_WhenProjectIsNotYours_ShouldBeNotFound()
    {
        var before = _ranker.Queries.Count;

        var reply = await SearchAsync("""{"words":"checkout","project":"PART"}""");

        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
        _ranker.Queries.Count.ShouldBe(before);
    }

    [Test]
    public async Task Search_WhenStateFilterWithoutProject_ShouldAskForProject()
    {
        var before = _ranker.Queries.Count;

        var reply = await SearchAsync("""{"words":"checkout","state":"Active"}""");

        AssertAsksForProject(reply);
        _ranker.Queries.Count.ShouldBe(before);
    }

    [Test]
    public async Task Search_WhenAssigneeFilterWithoutProject_ShouldAskForProject()
    {
        var before = _ranker.Queries.Count;

        var reply = await SearchAsync("""{"words":"checkout","assignee":"me"}""");

        AssertAsksForProject(reply);
        _ranker.Queries.Count.ShouldBe(before);
    }

    [Test]
    public async Task Search_WhenSprintFilterWithoutProject_ShouldAskForProject()
    {
        var before = _ranker.Queries.Count;

        var reply = await SearchAsync("""{"words":"checkout","sprint":"current"}""");

        AssertAsksForProject(reply);
        _ranker.Queries.Count.ShouldBe(before);
    }

    [Test]
    public async Task Search_WhenOnlyFiltersWithoutProject_ShouldUseRecentProject()
    {
        var before = _ranker.Queries.Count;

        var reply = await SearchAsync("""{"state":"New"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("project").GetString().ShouldNotBeNullOrEmpty();
        _ranker.Queries.Count.ShouldBe(before);
    }

    [Test]
    public async Task Search_WhenParentGiven_ShouldImplyProject()
    {
        var reply = await SearchAsync($$"""{"words":"routing","parent":"{{_featureKey}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var query = LastQuery;
        query.ProjectIds.ShouldBe([SeedIds.Projects.Ship]);
        var clause = query.Criteria.ShouldNotBeNull().Clauses.ShouldHaveSingleItem();
        clause.Field.ShouldBe(FieldIds.Parent);
        clause.Op.ShouldBe(CriteriaOp.Equal);
        clause.Value.GetString().ShouldBe(_feature.ToString());
        reply.Structured().GetProperty("project").GetString().ShouldBe("SHIP");
    }

    [Test]
    public async Task Search_WhenStateNamed_ShouldFilterByItsIdsAndIncludeClosed()
    {
        var reply = await SearchAsync("""{"words":"checkout","project":"SHIP","state":"Active","include_closed":false}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var query = LastQuery;
        var clause = query.Criteria.ShouldNotBeNull().Clauses.ShouldHaveSingleItem();
        clause.Field.ShouldBe(FieldIds.State);
        clause.Op.ShouldBe(CriteriaOp.In);
        clause.Value.EnumerateArray().ShouldNotBeEmpty();
        // The named state may be a completed one, which IncludeClosed = false would drop.
        query.IncludeClosed.ShouldBeTrue();
    }

    [Test]
    public async Task Search_WhenTypeAndStateAndWords_ShouldPinTheSingleStateId()
    {
        var reply = await SearchAsync("""{"words":"checkout","project":"SHIP","type":"task","state":"Active"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var clause = LastQuery.Criteria.ShouldNotBeNull().Clauses.ShouldHaveSingleItem();
        clause.Field.ShouldBe(FieldIds.State);
        clause.Op.ShouldBe(CriteriaOp.In);
        clause.Value.EnumerateArray().Count().ShouldBe(1);
    }

    [Test]
    public async Task Search_WhenNoLimit_ShouldDefaultTo10WithWordsAnd25Without()
    {
        await SearchAsync("""{"words":"checkout"}""");
        var withWords = LastQuery.Limit;
        var reply = await SearchAsync("""{"project":"SHIP","tag":"routing"}""");

        withWords.ShouldBe(10);
        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("matches").GetArrayLength().ShouldBe(2);
    }

    [Test]
    public async Task Search_WhenOnlyFilters_ShouldKeepTodaysOrder()
    {
        var before = _ranker.Queries.Count;

        var reply = await SearchAsync("""{"project":"SHIP","tag":"routing"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("matches").EnumerateArray().Select(m => m.GetProperty("key").GetString()).ShouldBe([_newerKey, _olderKey]);
        _ranker.Queries.Count.ShouldBe(before);
    }

    [Test]
    public async Task Search_WhenLimitOver25_ShouldCapAt25()
    {
        await SearchAsync("""{"words":"checkout","limit":100}""");
        var capped = LastQuery.Limit;
        await SearchAsync("""{"words":"checkout","limit":7}""");

        capped.ShouldBe(25);
        LastQuery.Limit.ShouldBe(7);
    }

    [Test]
    public async Task Search_ShouldCarryOpenUrlAndSnippet()
    {
        _ranker.Results = new SearchResults(
            true, [], [],
            [new SearchResultItem(Guid.NewGuid(), "SHIP-7", "Sign-in fails after password reset", WorkItemTypeId.Bug, "Active", StateCategory.InProgress, "SHIP", ["SHIP-2", "SHIP-3"],
                "Priya Shah", "Sprint 24", 3m, ["auth"], SearchField.Body, null, "The session cookie is dropped.", 0.5)],
            true);

        var reply = await SearchAsync("""{"words":"login broken"}""");

        var structured = reply.Structured();
        structured.GetProperty("semantic").GetBoolean().ShouldBeTrue();
        structured.GetProperty("more").GetBoolean().ShouldBeTrue();
        var hit = structured.GetProperty("matches").EnumerateArray().Single();
        hit.GetProperty("kind").GetString().ShouldBe("item");
        hit.GetProperty("key").GetString().ShouldBe("SHIP-7");
        hit.GetProperty("title").GetString().ShouldBe("Sign-in fails after password reset");
        hit.GetProperty("type").GetString().ShouldBe("Bug");
        hit.GetProperty("state").GetString().ShouldBe("Active");
        hit.GetProperty("project").GetString().ShouldBe("SHIP");
        hit.GetProperty("parent").GetString().ShouldBe("SHIP-3");
        hit.GetProperty("assignee").GetString().ShouldBe("Priya Shah");
        hit.GetProperty("sprint").GetString().ShouldBe("Sprint 24");
        hit.GetProperty("points").GetDecimal().ShouldBe(3m);
        hit.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ShouldBe(["auth"]);
        hit.GetProperty("matched_in").GetString().ShouldBe("body");
        hit.GetProperty("snippet").GetString().ShouldBe("The session cookie is dropped.");
        hit.GetProperty("open_url").GetString().ShouldEndWith("/p/SHIP/items/SHIP-7");
        hit.TryGetProperty("attachment", out _).ShouldBeFalse();
    }

    [Test]
    public async Task Search_WhenAttachmentHit_ShouldNameTheFile()
    {
        _ranker.Results = new SearchResults(
            false, [], [],
            [new SearchResultItem(Guid.NewGuid(), "SHIP-8", "Quiet feature", WorkItemTypeId.Feature, "New", StateCategory.InProgress, "SHIP", [], null, null, null, [],
                SearchField.Attachment, "spec.md", "The handshake needs two steps.", 0.4)],
            false);

        var reply = await SearchAsync("""{"words":"handshake"}""");

        var hit = reply.Structured().GetProperty("matches").EnumerateArray().Single();
        hit.GetProperty("matched_in").GetString().ShouldBe("attachment");
        hit.GetProperty("attachment").GetString().ShouldBe("spec.md");
    }

    [Test]
    public async Task Search_WhenSprintHit_ShouldShowNameAndGoal()
    {
        _ranker.Results = new SearchResults(
            false, [],
            [new SearchResultSprint(SeedIds.Sprints.Sprint24, "Sprint 24", "Ship the Jira importer", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 14), "SHIP")],
            [], false);

        var reply = await SearchAsync("""{"words":"importer"}""");

        var hit = reply.Structured().GetProperty("matches").EnumerateArray().Single();
        hit.GetProperty("kind").GetString().ShouldBe("sprint");
        hit.GetProperty("title").GetString().ShouldBe("Sprint 24: Ship the Jira importer");
        hit.GetProperty("goal").GetString().ShouldBe("Ship the Jira importer");
        hit.GetProperty("dates").GetString().ShouldBe("2026-06-01 to 2026-06-14");
        hit.GetProperty("project").GetString().ShouldBe("SHIP");
        hit.GetProperty("open_url").GetString().ShouldEndWith($"/p/SHIP/sprints/{SeedIds.Sprints.Sprint24}/taskboard");
    }

    [Test]
    public async Task Search_WhenProjectHit_ShouldCarryItsKeyAndBacklogUrl()
    {
        _ranker.Results = new SearchResults(false, [new SearchResultProject("SHIP", "ShipLean")], [], [], false);

        var reply = await SearchAsync("""{"words":"ship"}""");

        var hit = reply.Structured().GetProperty("matches").EnumerateArray().Single();
        hit.GetProperty("kind").GetString().ShouldBe("project");
        hit.GetProperty("key").GetString().ShouldBe("SHIP");
        hit.GetProperty("title").GetString().ShouldBe("ShipLean");
        hit.GetProperty("open_url").GetString().ShouldEndWith("/p/SHIP/backlogs/stories");
    }

    [Test]
    public async Task Search_WhenCommentHit_ShouldReturnItsItemWithCommentSnippet()
    {
        _ranker.Results = new SearchResults(
            false, [], [],
            [new SearchResultItem(Guid.NewGuid(), "SHIP-9", "Checkout page", WorkItemTypeId.Story, "New", StateCategory.InProgress, "SHIP", [], null, null, null, [],
                SearchField.Comment, null, "Customers hit a timeout after paying.", 0.3)],
            false);

        var reply = await SearchAsync("""{"words":"timeout after paying"}""");

        var hit = reply.Structured().GetProperty("matches").EnumerateArray().Single();
        hit.GetProperty("kind").GetString().ShouldBe("item");
        hit.GetProperty("key").GetString().ShouldBe("SHIP-9");
        hit.GetProperty("matched_in").GetString().ShouldBe("comment");
        hit.GetProperty("snippet").GetString().ShouldBe("Customers hit a timeout after paying.");
    }

    private SearchQuery LastQuery
    {
        get
        {
            var queries = _ranker.Queries;
            return queries[^1];
        }
    }

    private async Task<McpReply> SearchAsync(string arguments) => await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "search", arguments);

    private static void AssertAsksForProject(McpReply reply)
    {
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("project");
        error.GetProperty("message").GetString()!.ShouldContain("SHIP");
    }

    private static Task<string> KeyAsync(Npgsql.NpgsqlConnection connection, Guid id)
        => connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
}
