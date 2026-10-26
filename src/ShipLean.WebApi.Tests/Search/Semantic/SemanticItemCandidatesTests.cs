using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Search.Semantic;

[TestFixture]
public class SemanticItemCandidatesTests : SemanticFixture
{
    [Test]
    public async Task Semantic_WhenRowAdded_ShouldSeeItOnNextQuery()
    {
        await using var connection = await OpenDatabaseAsync();
        var first = await ItemAsync(connection, "Sign-in fails for SSO users");
        await RowAsync(connection, first, SearchField.Title, "Sign-in fails for SSO users");
        (await ListsAsync("login broken")).Lists.SelectMany(l => l.Hits).Select(h => h.WorkItemId).ShouldBe([first]);

        var second = await ItemAsync(connection, "Authentication crashes on mobile");
        await RowAsync(connection, second, SearchField.Title, "Authentication crashes on mobile");

        Ids(await ListsAsync("login broken"), RankedListName.TitleVector).ShouldBe([first, second], ignoreOrder: true);
    }

    [Test]
    public async Task Semantic_WhenRowDeleted_ShouldDropItOnNextQuery()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "Sign-in fails for SSO users");
        var row = await RowAsync(connection, item, SearchField.Title, "Sign-in fails for SSO users");
        Ids(await ListsAsync("login broken"), RankedListName.TitleVector).ShouldBe([item]);

        await ExecuteAsync(connection, "DELETE FROM search_doc WHERE id = @row", new { row });

        Ids(await ListsAsync("login broken"), RankedListName.TitleVector).ShouldBeEmpty();
    }

    [Test]
    public async Task Semantic_WhenEmbeddingWrittenWithoutMovingIndexedAt_ShouldSeeItOnNextQuery()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "Sign-in fails for SSO users");
        var row = await RowAsync(connection, item, SearchField.Title, "Sign-in fails for SSO users", embedded: false);
        Ids(await ListsAsync("login broken"), RankedListName.TitleVector).ShouldBeEmpty();

        var vector = (await Embedder.EmbedPassagesAsync(["Sign-in fails for SSO users"]))[0];
        await ExecuteAsync(connection, "UPDATE search_doc SET embedding = @vector, embedding_model = @model WHERE id = @row", new { vector, model = Embedder.ModelId!.ToString(), row });

        Ids(await ListsAsync("login broken"), RankedListName.TitleVector).ShouldBe([item]);
    }

    [Test]
    public async Task Semantic_WhenBelowCutOff_ShouldLeaveItOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var near = await ItemAsync(connection, "Sign-in fails for SSO users");
        await RowAsync(connection, near, SearchField.Title, "Sign-in fails for SSO users");
        var far = await ItemAsync(connection, "Banana smoothie recipe");
        await RowAsync(connection, far, SearchField.Title, "Banana smoothie recipe");

        var lists = await ListsAsync("login broken");

        Ids(lists, RankedListName.TitleVector).ShouldBe([near]);
        lists.Lists.SelectMany(l => l.Hits).ShouldAllBe(h => h.Score >= 0.55);
    }

    [Test]
    public async Task Semantic_WhenRowsUnembedded_ShouldReportPending()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "Sign-in fails for SSO users");
        await RowAsync(connection, item, SearchField.Title, "Sign-in fails for SSO users");
        await RowAsync(connection, item, SearchField.Body, "Sign-in fails for SSO users\nbody", embedded: false);
        await RowAsync(connection, item, SearchField.Tags, "auth", embedded: false);
        // Embedded by another model: stale, so pending.
        var comment = Guid.NewGuid();
        var stale = (await Embedder.EmbedPassagesAsync(["x"]))[0];
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Comment, comment, SearchField.Comment, "Sign-in fails for SSO users\nhi")
            .WithWorkItem(item).WithEmbedding(stale, new EmbeddingModelId("other-model", "9")).InsertAsync(connection);
        // Not counted: a removed item's row, and a project the caller is not in.
        var removed = await ItemAsync(connection, "Gone", stateId: SeedIds.States.Ship.StoryRemoved);
        await RowAsync(connection, removed, SearchField.Title, "Gone", embedded: false);
        var hidden = await ItemAsync(connection, "Hidden", SeedIds.Projects.Part);
        await RowAsync(connection, hidden, SearchField.Title, "Hidden", embedded: false, projectId: SeedIds.Projects.Part);

        var lists = await ListsAsync("login broken", true, SeedIds.Projects.Ship, SeedIds.Projects.Part);

        lists.Available.ShouldBeTrue();
        lists.PendingRows.ShouldBe(2);
        Ids(lists, RankedListName.BodyVector).ShouldBeEmpty();
    }

    [Test]
    public async Task Semantic_WhenQueryHasNoVector_ShouldBeUnavailable()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "Sign-in fails for SSO users");
        await RowAsync(connection, item, SearchField.Title, "Sign-in fails for SSO users");

        var lists = await ListsAsync("login broken", vector: false);

        lists.Available.ShouldBeFalse();
        lists.Lists.ShouldBeEmpty();
    }

    [Test]
    public async Task Semantic_WhenBodyChunkHits_ShouldSnippetAfterTheTitleLine()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "Unrelated heading");
        await RowAsync(connection, item, SearchField.Body, "Unrelated heading\nUsers cannot sign in after the reset", embedAs: "sign in broken");
        var comment = await ItemAsync(connection, "Another heading");
        await RowAsync(connection, comment, SearchField.Comment, "no newline comment", kind: SearchSourceKind.Comment, sourceId: Guid.NewGuid(), embedAs: "sign in broken");

        var hits = (await ListsAsync("login broken")).Lists.Single(l => l.Name == RankedListName.BodyVector).Hits;

        hits.Single(h => h.WorkItemId == item).Snippet.ShouldBe("Users cannot sign in after the reset");
        hits.Single(h => h.WorkItemId == item).Field.ShouldBe(SearchField.Body);
        hits.Single(h => h.WorkItemId == comment).Snippet.ShouldBe("no newline comment");
    }

    [Test]
    public async Task Semantic_WhenAttachmentChunkHits_ShouldCarryTheFileName()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "Spec holder");
        var attachment = Guid.NewGuid();
        await ExecuteAsync(
            connection,
            "INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref) VALUES (@attachment, @item, 'spec.pdf', 'application/pdf', 3, 'blob/a')",
            new { attachment, item });
        await RowAsync(connection, item, SearchField.Attachment, "Spec holder\nlogin broken", kind: SearchSourceKind.Attachment, sourceId: attachment);

        var hit = (await ListsAsync("login broken")).Lists.Single(l => l.Name == RankedListName.BodyVector).Hits.Single();

        hit.Field.ShouldBe(SearchField.Attachment);
        hit.Attachment.ShouldBe("spec.pdf");
        hit.Snippet.ShouldBe("login broken");
    }

    [Test]
    public async Task Semantic_WhenItemHasTwoChunks_ShouldKeepTheBestOne()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "Heading");
        await RowAsync(connection, item, SearchField.Body, "Heading\nweak chunk", embedAs: "login broken sluggish payment");
        await RowAsync(connection, item, SearchField.Body, "Heading\nstrong chunk", embedAs: "login broken", chunk: 1);

        var hits = (await ListsAsync("login broken")).Lists.Single(l => l.Name == RankedListName.BodyVector).Hits;

        hits.Single().Snippet.ShouldBe("strong chunk");
    }

    [Test]
    public async Task Semantic_WhenTypesAndTagsAsked_ShouldKeepOnlyMatchingItems()
    {
        await using var connection = await OpenDatabaseAsync();
        var match = await ItemAsync(connection, "Sign-in fails for SSO users", type: "bug", tags: ["Auth", "urgent"]);
        var wrongType = await ItemAsync(connection, "Sign-in fails on mobile", type: "story", tags: ["auth", "urgent"]);
        var missingTag = await ItemAsync(connection, "Sign-in fails for guests", type: "bug", tags: ["auth"]);
        foreach (var item in new[] { match, wrongType, missingTag })
        {
            await RowAsync(connection, item, SearchField.Title, "Sign-in fails for SSO users");
        }

        var lists = await ListsAsync("login broken", true, [SeedIds.Projects.Ship], q => q with { Types = [WorkItemTypeId.Bug], Tags = ["AUTH", "Urgent"] });

        Ids(lists, RankedListName.TitleVector).ShouldBe([match]);
    }
}
