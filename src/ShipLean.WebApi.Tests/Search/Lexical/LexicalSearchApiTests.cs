using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Lexical;

// GET /api/search over the real lexical leg and rows arranged with SearchDocBuilder (no indexer). The semantic leg is unavailable, so
// the lexical lists are all there is. Presence and fields only: order is the ranker's.
[TestFixture]
public class LexicalSearchApiTests : ApiFixture
{
    [Test]
    public async Task Search_WhenNotMember_ShouldNotReturnProjectItems()
    {
        await using var connection = await OpenDatabaseAsync();
        var hidden = await ArrangeItemAsync(connection, SeedIds.Projects.Part, "memberwall hidden");
        var shown = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "memberwall shown");

        var keys = await KeysAsync("q=memberwall");

        keys.ShouldContain(shown.Key);
        keys.ShouldNotContain(hidden.Key);
    }

    [Test]
    public async Task Search_WhenPrefixTyped_ShouldMatchPartialWord()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "Reconciliation of invoices");

        (await KeysAsync("q=reconcil")).ShouldNotContain(item.Key);
        (await KeysAsync("q=reconcil&prefix=true")).ShouldContain(item.Key);
        (await KeysAsync("q=invoices%20reconcil&prefix=true")).ShouldContain(item.Key);
        (await KeysAsync("q=payments%20reconcil&prefix=true")).ShouldNotContain(item.Key);
    }

    [Test]
    public async Task Search_WhenItemRemoved_ShouldLeaveItOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var removed = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "vanishing act", SeedIds.States.Ship.StoryRemoved);
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Comment, Guid.NewGuid(), SearchField.Comment, "vanishing comment").WithWorkItem(removed.Id).InsertAsync(connection);
        var kept = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "vanishing kept");

        var keys = await KeysAsync("q=vanishing");

        keys.ShouldContain(kept.Key);
        keys.ShouldNotContain(removed.Key);
    }

    [Test]
    public async Task Search_WhenKeyTyped_ShouldMatchSimpleConfig()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "Mentions SHIP-9876 in passing");

        (await KeysAsync("q=ship-9876")).ShouldContain(item.Key);
    }

    [Test]
    public async Task Search_WhenCommentMatches_ShouldSayMatchedInComment()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "Plain title");
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Comment, Guid.NewGuid(), SearchField.Comment, "Plain title\nthe zeppelin kept drifting").WithWorkItem(item.Id).InsertAsync(connection);

        var hit = (await ItemsAsync("q=zeppelin")).Single(i => i.GetProperty("key").GetString() == item.Key);

        hit.GetProperty("matchedIn").GetString().ShouldBe("comment");
        var snippet = hit.GetProperty("snippet").GetString()!;
        snippet.ShouldContain("zeppelin");
        snippet.ShouldNotContain("<");
        snippet.ShouldNotStartWith("Plain title");
    }

    [Test]
    public async Task Search_WhenOnlyTitleMatches_ShouldGiveNoBodyListHit()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "Gazebo repairs");
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Comment, Guid.NewGuid(), SearchField.Comment, "Gazebo repairs\nnothing relevant said").WithWorkItem(item.Id).InsertAsync(connection);
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Item, item.Id, SearchField.Body, "Gazebo repairs\nthe roof leaks").WithWorkItem(item.Id).InsertAsync(connection);

        var lists = await ListsAsync("gazebo");

        lists.Single(l => l.Name == RankedListName.TitleLexical).Hits.Select(h => h.WorkItemId).ShouldBe([item.Id]);
        lists.Single(l => l.Name == RankedListName.BodyLexical).Hits.ShouldBeEmpty();
    }

    [Test]
    public async Task Search_WhenPrefixFollowsOr_ShouldKeepTheOr()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "Pumpernickel baking");

        (await KeysAsync("q=pumpernickel%20or%20sourdo&prefix=true")).ShouldContain(item.Key);
        (await KeysAsync("q=pumpernickel%20sourdo&prefix=true")).ShouldNotContain(item.Key);
    }

    [Test]
    public async Task Search_WhenPrefixIsThaiWord_ShouldKeepTheWordWhole()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "ค้นหาไฟล์ thaiprefix");

        (await KeysAsync("q=thaiprefix%20" + Uri.EscapeDataString("ค้นห") + "&prefix=true")).ShouldContain(item.Key);
    }

    [Test]
    public async Task Search_WhenAttachmentMatches_ShouldSayMatchedInAttachmentWithFileName()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "Has a file");
        var attachmentId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref) VALUES (@id, @workItemId, 'notes-quokka.md', 'text/markdown', 10, 'ref')",
            new { id = attachmentId, workItemId = item.Id });
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Attachment, attachmentId, SearchField.Attachment, "Has a file\nquokka sightings").WithWorkItem(item.Id).InsertAsync(connection);

        var hit = (await ItemsAsync("q=quokka")).Single(i => i.GetProperty("key").GetString() == item.Key);

        hit.GetProperty("matchedIn").GetString().ShouldBe("attachment");
        hit.GetProperty("attachment").GetString().ShouldBe("notes-quokka.md");
    }

    [Test]
    public async Task Search_WhenTagGiven_ShouldNarrowAcrossProjects()
    {
        await using var connection = await OpenDatabaseAsync();
        var inShip = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "narrowing ship", tags: ["Urgent", "web"]);
        var inPay = await ArrangeItemAsync(connection, SeedIds.Projects.Pay, "narrowing pay", tags: ["urgent"]);
        var untagged = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "narrowing plain");
        var oneTag = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "narrowing web only", tags: ["web"]);

        // The endpoint has no tags parameter (CB5), so this goes through the ranker, which the MCP search will call with Tags.
        (await RankedKeysAsync(null)).ShouldBe([inShip.Key, inPay.Key, untagged.Key, oneTag.Key], ignoreOrder: true);
        (await RankedKeysAsync(["urgent"])).ShouldBe([inShip.Key, inPay.Key], ignoreOrder: true);
        (await RankedKeysAsync(["urgent", "web"])).ShouldBe([inShip.Key]);
    }

    [Test]
    public async Task Search_WhenItemClosed_ShouldLeaveItOutUnlessAsked()
    {
        await using var connection = await OpenDatabaseAsync();
        var closed = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "finished business", SeedIds.States.Ship.StoryClosed);

        (await KeysAsync("q=finished")).ShouldNotContain(closed.Key);
        (await KeysAsync("q=finished&closed=true")).ShouldContain(closed.Key);
    }

    [Test]
    public async Task Search_WhenTypesGiven_ShouldKeepOnlyThoseTypes()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "typed thing");
        var bug = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "typed bug", type: "bug");

        (await KeysAsync("q=typed&types=Bug")).ShouldBe([bug.Key]);
        (await KeysAsync("q=typed")).ShouldBe([story.Key, bug.Key], ignoreOrder: true);
    }

    [Test]
    public async Task Search_WhenCriteriaGiven_ShouldApplyThemInSql()
    {
        await using var connection = await OpenDatabaseAsync();
        var plain = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "criteria plain");
        var bug = await ArrangeItemAsync(connection, SeedIds.Projects.Ship, "criteria bug", type: "bug");
        var criteria = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause("type", CriteriaOp.Equal, JsonSerializer.SerializeToElement("bug"))]);

        using var scope = Host.Services.CreateScope();
        var results = await scope.ServiceProvider.GetRequiredService<ISearchRanker>()
            .SearchAsync(new SearchQuery("criteria", [SeedIds.Projects.Ship], Criteria: criteria, Limit: 25));

        results.Items.Select(i => i.Key).ShouldBe([bug.Key]);
        results.Items.ShouldNotContain(i => i.Key == plain.Key);
    }

    private async Task<IReadOnlyList<RankedList>> ListsAsync(string text)
    {
        using var scope = Host.Services.CreateScope();
        var lexical = scope.ServiceProvider.GetServices<IItemCandidates>().Single(c => c.Leg == CandidateLeg.Lexical);
        var lists = await lexical.ListsAsync(new CandidateQuery(text, [SeedIds.Projects.Ship], null, null, false, false, null, null));
        return lists.Lists;
    }

    private async Task<List<string>> RankedKeysAsync(string[]? tags)
    {
        using var scope = Host.Services.CreateScope();
        var results = await scope.ServiceProvider.GetRequiredService<ISearchRanker>().SearchAsync(new SearchQuery("narrowing", Tags: tags, Limit: 25));
        return [.. results.Items.Select(i => i.Key)];
    }

    private async Task<List<string>> KeysAsync(string query)
        => [.. (await ItemsAsync(query)).Select(i => i.GetProperty("key").GetString()!)];

    private async Task<List<JsonElement>> ItemsAsync(string query)
    {
        using var response = await Api.GetAsync($"api/search?{query}");
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        return [.. JsonDocument.Parse(text).RootElement.GetProperty("items").EnumerateArray().Select(i => i.Clone())];
    }

    private static async Task<(Guid Id, string Key)> ArrangeItemAsync(NpgsqlConnection connection, Guid projectId, string title, Guid? stateId = null, string[]? tags = null, string type = "story")
    {
        var builder = new WorkItemBuilder(projectId).WithTitle(title).WithType(type).WithTags(tags ?? []);
        if (stateId is not null)
        {
            builder.WithState(stateId.Value);
        }

        var id = await builder.InsertAsync(connection);
        await new SearchDocBuilder(projectId, SearchSourceKind.Item, id, SearchField.Title, title).WithWorkItem(id).InsertAsync(connection);
        var key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
        return (id, key!);
    }
}
