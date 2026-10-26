using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.ProjectSettings.Api;
using ShipLean.WebApi.Tests.Search.Endpoint;

namespace ShipLean.WebApi.Tests.Search.Ranking;

// The ranker over scripted legs and real work items, through GET /api/search (and ISearchRanker for criteria).
[TestFixture]
public class SearchRankerApiTests : ApiFixture
{
    private readonly ScriptedItemCandidates _lexical = new(CandidateLeg.Lexical);
    private readonly ScriptedItemCandidates _semantic = new(CandidateLeg.Semantic);
    private readonly StubSearchGroups _groups = new();
    private readonly CapturingLogger<SearchRanker> _logs = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IItemCandidates>();
        services.AddSingleton<IItemCandidates>(_lexical);
        services.AddSingleton<IItemCandidates>(_semantic);
        services.Replace(ServiceDescriptor.Singleton<ISearchGroups>(_groups));
        services.Replace(ServiceDescriptor.Singleton<ILogger<SearchRanker>>(_logs));
    };

    [SetUp]
    public void ResetLegs()
    {
        _lexical.Lists = [];
        _semantic.Lists = [];
        _semantic.Available = true;
        _semantic.PendingRows = 0;
        _groups.Projects = [];
        _groups.Sprints = [];
    }

    [TestCase("")]
    [TestCase("lower")]
    [TestCase("nodash")]
    public async Task Search_WhenExactKey_ShouldRankItFirst(string spelling)
    {
        var listed = await InsertAsync(b => b.WithTitle("Rank listed"));
        var named = await InsertAsync(b => b.WithTitle("Rank named"));
        var key = await KeyAsync(named);
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(listed, "listed")])];

        var query = spelling switch
        {
            "lower" => key.ToLowerInvariant(),
            "nodash" => key.Replace("-", "").ToLowerInvariant(),
            _ => key,
        };
        var keys = await SearchKeysAsync($"q={query}");

        keys.ShouldBe([key, await KeyAsync(listed)]);
    }

    [Test]
    public async Task Search_WhenExactItemIsAlsoListedLower_ShouldMoveItFirstOnce()
    {
        var other = await InsertAsync(b => b.WithTitle("Rank other first"));
        var named = await InsertAsync(b => b.WithTitle("Rank named lower"));
        var key = await KeyAsync(named);
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(other, "other"), StubItemCandidates.Hit(named, "named")])];

        var keys = await SearchKeysAsync($"q={key}");

        keys.ShouldBe([key, await KeyAsync(other)]);
    }

    [Test]
    public async Task Search_WhenExactKeyOutsideCriteria_ShouldNotPutItFirst()
    {
        var listed = await InsertAsync(b => b.WithTitle("Rank criteria listed"));
        var named = await InsertAsync(b => b.WithTitle("Rank criteria named"));
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(listed, "listed")])];
        var criteria = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause("state", CriteriaOp.Equal, JsonSerializer.SerializeToElement("Done"))]);

        var results = await SearchDirectAsync(new SearchQuery(await KeyAsync(named), [SeedIds.Projects.Ship], Criteria: criteria));

        results.Items.Select(i => i.Key).ShouldBe([await KeyAsync(listed)]);
    }

    [Test]
    public async Task Search_WhenExactKeyAndTypesListedIt_ShouldStillPutItFirst()
    {
        var other = await InsertAsync(b => b.WithTitle("Rank types other"));
        var named = await InsertAsync(b => b.WithTitle("Rank types named"));
        var key = await KeyAsync(named);
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(other, "other"), StubItemCandidates.Hit(named, "named")])];

        var keys = await SearchKeysAsync($"q={key}&types=story");

        keys.ShouldBe([key, await KeyAsync(other)]);
    }

    [Test]
    public async Task Search_WhenExactKeyIsInAProjectTheCallerIsNotIn_ShouldReturnNothing()
    {
        string key;
        Guid projectId;
        await using (var connection = await OpenDatabaseAsync())
        {
            var project = await ArrangedProject.CreateAsync(connection);
            projectId = project.Id;
            var itemId = await new WorkItemBuilder(project.Id).WithType("story").WithTitle("Rank not a member").InsertAsync(connection);
            key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @itemId", new { itemId });
        }

        var results = await SearchDirectAsync(new SearchQuery(key, [projectId]));

        results.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Search_WhenExactKeyIsRemoved_ShouldLeaveItOut()
    {
        var removed = await InsertAsync(b => b.WithTitle("Rank removed").WithStateNamed("Removed"));

        var keys = await SearchKeysAsync($"q={await KeyAsync(removed)}");

        keys.ShouldBeEmpty();
    }

    [Test]
    public async Task Search_WhenExactKeyIsInAnotherProject_ShouldLeaveItOut()
    {
        var named = await InsertAsync(b => b.WithTitle("Rank other project"));
        var key = await KeyAsync(named);

        var keys = await SearchKeysAsync($"q={key}&project=PAY");

        keys.ShouldBeEmpty();
    }

    [Test]
    public async Task Search_WhenClosed_ShouldRankBelowOpen()
    {
        var closed = await InsertAsync(b => b.WithTitle("Rank closed").WithStateNamed("Closed"));
        var open = await InsertAsync(b => b.WithTitle("Rank open"));
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(closed, "closed"), StubItemCandidates.Hit(open, "open")])];

        var keys = await SearchKeysAsync("q=rank&closed=true");

        keys.ShouldBe([await KeyAsync(open), await KeyAsync(closed)]);
    }

    [Test]
    public async Task Search_WhenAssignedToMe_ShouldRankItAboveTheNextHit()
    {
        var other = await InsertAsync(b => b.WithTitle("Rank other"));
        var mine = await InsertAsync(b => b.WithTitle("Rank mine").WithAssignee(SeedIds.Users.Joel));
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(other, "other"), StubItemCandidates.Hit(mine, "mine")])];

        var keys = await SearchKeysAsync("q=rank");

        keys.ShouldBe([await KeyAsync(mine), await KeyAsync(other)]);
    }

    [Test]
    public async Task Search_WhenClosedFalse_ShouldLeaveClosedOut()
    {
        var closed = await InsertAsync(b => b.WithTitle("Rank closed out").WithStateNamed("Closed"));
        var open = await InsertAsync(b => b.WithTitle("Rank open in"));
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(closed, "closed"), StubItemCandidates.Hit(open, "open")])];

        var keys = await SearchKeysAsync("q=rank");

        keys.ShouldBe([await KeyAsync(open)]);
    }

    [Test]
    public async Task Search_WhenMoreThanLimit_ShouldSayMore()
    {
        var hits = new List<CandidateHit>();
        foreach (var n in Enumerable.Range(0, 3))
        {
            hits.Add(StubItemCandidates.Hit(await InsertAsync(b => b.WithTitle($"Rank page {n}")), "page"));
        }

        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, hits)];

        var root = await ReadAsync("q=rank&limit=2");

        root.GetProperty("items").GetArrayLength().ShouldBe(2);
        root.GetProperty("more").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task Search_WhenExactlyLimitHits_ShouldSayNoMore()
    {
        var hits = new List<CandidateHit>();
        foreach (var n in Enumerable.Range(0, 2))
        {
            hits.Add(StubItemCandidates.Hit(await InsertAsync(b => b.WithTitle($"Rank exact page {n}")), "page"));
        }

        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, hits)];

        var root = await ReadAsync("q=rank&limit=2");

        root.GetProperty("items").GetArrayLength().ShouldBe(2);
        root.GetProperty("more").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task Search_WhenALegListsAnItemOutsideTheProjects_ShouldThrow()
    {
        var stray = await InsertAsync(b => b.WithTitle("Rank stray"));
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(stray, "stray")])];

        await Should.ThrowAsync<InvalidOperationException>(() => SearchDirectAsync(new SearchQuery("rank", [SeedIds.Projects.Pay])));
    }

    [Test]
    public async Task Search_WhenSemanticLegAnswersWithNothingPending_ShouldSaySemanticTrue()
    {
        var before = FallbackLines().Count;

        var root = await ReadAsync("q=rank");

        root.GetProperty("semantic").GetBoolean().ShouldBeTrue();
        FallbackLines().Count.ShouldBe(before);
    }

    [Test]
    public async Task Search_WhenRowsPending_ShouldSaySemanticFalse()
    {
        var item = await InsertAsync(b => b.WithTitle("Rank pending"));
        _semantic.PendingRows = 4;
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(item, "pending")])];
        var before = FallbackLines().Count;

        var root = await ReadAsync("q=rank");

        root.GetProperty("semantic").GetBoolean().ShouldBeFalse();
        root.GetProperty("items").GetArrayLength().ShouldBe(1);
        var logged = FallbackLines().Skip(before).ToList();
        logged.Count.ShouldBe(1);
        logged[0].ShouldContain("4");
        FallbackLevels().Skip(before).ShouldBe([LogLevel.Debug]);
    }

    [Test]
    public async Task Search_WhenSemanticLegUnavailable_ShouldSaySemanticFalseAndLogOnce()
    {
        _semantic.Available = false;
        var before = FallbackLines().Count;

        var root = await ReadAsync("q=rank");

        root.GetProperty("semantic").GetBoolean().ShouldBeFalse();
        FallbackLevels().Skip(before).ShouldBe([LogLevel.Warning]);
    }

    [Test]
    public async Task Search_WhenManyGroupsMatch_ShouldCapAtFive()
    {
        _groups.Projects = [.. Enumerable.Range(1, 7).Select(n => new SearchResultProject($"P{n}", $"Project {n}"))];
        _groups.Sprints = [.. Enumerable.Range(1, 7).Select(n => new SearchResultSprint(Guid.NewGuid(), $"Sprint {n}", "", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 14), "SHIP"))];

        var root = await ReadAsync("q=rank");

        root.GetProperty("projects").GetArrayLength().ShouldBe(5);
        root.GetProperty("sprints").GetArrayLength().ShouldBe(5);
    }

    [Test]
    public async Task Search_WhenItemFiltersGiven_ShouldReturnNoGroups()
    {
        _groups.Projects = [new SearchResultProject("SHIP", "ShipLean")];
        _groups.Sprints = [new SearchResultSprint(Guid.NewGuid(), "Sprint 1", "", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 14), "SHIP")];

        var root = await ReadAsync("q=rank&types=story");

        root.GetProperty("projects").GetArrayLength().ShouldBe(0);
        root.GetProperty("sprints").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public void Search_WhenCriteriaWithManyProjects_ShouldBeValidation()
    {
        using var scope = Host.Services.CreateScope();
        var ranker = scope.ServiceProvider.GetRequiredService<ISearchRanker>();
        var criteria = new CriteriaJson(CriteriaMatch.All, []);

        var thrown = Should.Throw<ApiException>(() => ranker.SearchAsync(new SearchQuery("rank", [SeedIds.Projects.Ship, SeedIds.Projects.Pay], Criteria: criteria)));

        thrown.Code.ShouldBe("validation");
    }

    private List<LogLevel> FallbackLevels() => [.. _logs.Lines.Where(l => l.Message.StartsWith("Search is lexical only", StringComparison.Ordinal)).Select(l => l.Level)];

    private async Task<SearchResults> SearchDirectAsync(SearchQuery query)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISearchRanker>().SearchAsync(query);
    }

    private List<string> FallbackLines() => [.. _logs.Lines.Where(l => l.Message.StartsWith("Search is lexical only", StringComparison.Ordinal)).Select(l => l.Message)];

    private async Task<Guid> InsertAsync(Func<WorkItemBuilder, WorkItemBuilder> configure)
    {
        await using var connection = await OpenDatabaseAsync();
        return await configure(new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story")).InsertAsync(connection);
    }

    private async Task<string> KeyAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
    }

    private async Task<List<string>> SearchKeysAsync(string query)
    {
        var root = await ReadAsync(query);
        return [.. root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!)];
    }

    private async Task<JsonElement> ReadAsync(string query)
    {
        using var response = await Api.GetAsync($"api/search?{query}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
