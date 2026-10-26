using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Endpoint;

// GET /api/search over stub legs and groups and real work items: the fusion, the paging and the element shapes on the wire.
[TestFixture]
public class SearchEndpointFusionApiTests : ApiFixture
{
    private static readonly Guid SprintId = SeedIds.Sprints.Sprint23;

    private readonly StubItemCandidates _lexical = new(CandidateLeg.Lexical);
    private readonly StubItemCandidates _semantic = new(CandidateLeg.Semantic);
    private readonly StubSearchGroups _groups = new()
    {
        Projects = [new SearchResultProject("SHIP", "ShipLean")],
        Sprints = [new SearchResultSprint(SprintId, "Sprint 23", "Ship search", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 25), "SHIP")],
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IItemCandidates>();
        services.AddSingleton<IItemCandidates>(_lexical);
        services.AddSingleton<IItemCandidates>(_semantic);
        services.Replace(ServiceDescriptor.Singleton<ISearchGroups>(_groups));
    };

    [Test]
    public async Task Search_WhenAnItemIsInTwoLists_ShouldRankItFirstAndReportMoreAtTheLimit()
    {
        var items = await ArrangeAsync();

        using var response = await Api.GetAsync("api/search?q=login&limit=2");

        var root = await ReadAsync(response);
        root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()).ShouldBe([items.InBoth.Key, items.InOne.Key]);
        root.GetProperty("more").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task Search_WhenEveryHitFits_ShouldNotReportMore()
    {
        var items = await ArrangeAsync();

        using var response = await Api.GetAsync("api/search?q=login&limit=3");

        var root = await ReadAsync(response);
        root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()).ShouldBe([items.InBoth.Key, items.InOne.Key, items.InSemanticOnly.Key]);
        root.GetProperty("more").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task Search_WhenHitsResolve_ShouldWriteTheItemSprintAndProjectElements()
    {
        var items = await ArrangeAsync();

        using var response = await Api.GetAsync("api/search?q=login");

        var root = await ReadAsync(response);
        var item = root.GetProperty("items")[0];
        item.EnumerateObject().Select(p => p.Name).ShouldBe(["key", "title", "type", "state", "projectKey", "parentPath", "matchedIn", "snippet", "url", "attachment"]);
        item.GetProperty("key").GetString().ShouldBe(items.InBoth.Key);
        item.GetProperty("title").GetString().ShouldBe("Fusion both");
        item.GetProperty("type").GetString().ShouldBe("story");
        item.GetProperty("projectKey").GetString().ShouldBe("SHIP");
        item.GetProperty("parentPath").EnumerateArray().Select(p => p.GetString()).ShouldBe([items.Epic, items.Feature]);
        item.GetProperty("matchedIn").GetString().ShouldBe("title");
        item.GetProperty("snippet").GetString().ShouldBe("both snippet");
        item.GetProperty("url").GetString().ShouldBe($"/p/SHIP/items/{items.InBoth.Key}");
        item.GetProperty("attachment").ValueKind.ShouldBe(JsonValueKind.Null);
        var attached = root.GetProperty("items")[2];
        attached.GetProperty("matchedIn").GetString().ShouldBe("attachment");
        attached.GetProperty("attachment").GetString().ShouldBe("spec.pdf");

        var sprint = root.GetProperty("sprints")[0];
        sprint.EnumerateObject().Select(p => p.Name).ShouldBe(["name", "goal", "startDate", "endDate", "projectKey", "url"]);
        sprint.GetProperty("name").GetString().ShouldBe("Sprint 23");
        sprint.GetProperty("startDate").GetString().ShouldBe("2026-10-12");
        sprint.GetProperty("endDate").GetString().ShouldBe("2026-10-25");
        sprint.GetProperty("url").GetString().ShouldBe($"/p/SHIP/sprints/{SprintId}/taskboard");

        var project = root.GetProperty("projects")[0];
        project.EnumerateObject().Select(p => p.Name).ShouldBe(["key", "name", "url"]);
        project.GetProperty("url").GetString().ShouldBe("/p/SHIP/backlogs/stories");
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    // Lexical: [inOne, inBoth]. Semantic: [inBoth, semanticOnly]. Fused: inBoth, inOne, semanticOnly.
    private async Task<Arranged> ArrangeAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var epic = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("epic").WithTitle("Fusion epic").InsertAsync(connection);
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithParent(epic).WithTitle("Fusion feature").InsertAsync(connection);
        var both = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithParent(feature).WithTitle("Fusion both").InsertAsync(connection);
        var one = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Fusion one").InsertAsync(connection);
        var semanticOnly = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bug").WithTitle("Fusion semantic").InsertAsync(connection);
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(one, "one snippet"), StubItemCandidates.Hit(both, "both snippet")])];
        _semantic.Lists = [new RankedList(RankedListName.TitleVector, [StubItemCandidates.Hit(both, "both snippet"), StubItemCandidates.Hit(semanticOnly, "semantic snippet", "spec.pdf")])];
        return new Arranged(
            await KeyAsync(connection, epic),
            await KeyAsync(connection, feature),
            new Keyed(await KeyAsync(connection, both)),
            new Keyed(await KeyAsync(connection, one)),
            new Keyed(await KeyAsync(connection, semanticOnly)));
    }

    private static Task<string> KeyAsync(System.Data.Common.DbConnection connection, Guid id)
        => connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });

    private sealed record Keyed(string Key);

    private sealed record Arranged(string Epic, string Feature, Keyed InBoth, Keyed InOne, Keyed InSemanticOnly);
}
