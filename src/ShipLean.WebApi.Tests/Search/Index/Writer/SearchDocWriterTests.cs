using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Index.Writer;

// SearchDocWriter with the real work item text source: rows are arranged with builders and direct SQL, the writer is called
// directly, and the assertions read search_doc. No background pass exists to disturb them (B2d adds it; nothing here depends on it).
[TestFixture]
public class SearchDocWriterTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    [Test]
    public async Task Index_WhenItemSynced_ShouldWriteTitleTagsAndBodyRows()
    {
        var item = await ArrangeItemAsync("Checkout fails", tags: ["payments", "urgent"], description: "Cart empties.",
            criteria: """[{"text":"Cart keeps items","done":false},{"text":"Totals add up","done":true}]""", repro: "1. Add an item");

        await SyncAsync(item);

        var rows = await RowsAsync(item);
        rows.Select(r => (r.Field, r.ChunkNo)).ShouldBe([("body", 0), ("tags", 0), ("title", 0)], ignoreOrder: true);
        rows.Single(r => r.Field == "title").Text.ShouldBe("Checkout fails");
        rows.Single(r => r.Field == "tags").Text.ShouldBe("payments urgent");
        rows.Single(r => r.Field == "body").Text.ShouldBe("Checkout fails\nCart empties.\n\nCart keeps items\n\nTotals add up\n\n1. Add an item");
        foreach (var row in rows)
        {
            row.SourceKind.ShouldBe("item");
            row.SourceId.ShouldBe(item);
            row.WorkItemId.ShouldBe(item);
            row.ProjectId.ShouldBe(Project);
            row.TextHash.ShouldBe(SearchDocBuilder.HashOf(row.Text));
            row.Embedded.ShouldBeFalse();
        }
        // The title and tags are plain; only the body is prefixed with the title.
        (await TsvAsync(item, "title")).ShouldContain("'checkout':1A");
        (await TsvAsync(item, "tags")).ShouldContain("'urgent':2B");
    }

    [Test]
    public async Task Index_WhenItemHasNoTagsOrBody_ShouldWriteOnlyTheTitle()
    {
        var item = await ArrangeItemAsync("Bare item");

        await SyncAsync(item);

        (await RowsAsync(item)).Select(r => r.Field).ShouldBe(["title"]);
    }

    [Test]
    public async Task Index_WhenTextUnchanged_ShouldKeepEmbedding()
    {
        var item = await ArrangeItemAsync("Stable title", description: "Stable body.");
        await SyncAsync(item);
        await EmbedAllAsync(item);
        var before = await RowsAsync(item);

        await SyncAsync(item);

        var after = await RowsAsync(item);
        after.ShouldAllBe(r => r.Embedded && r.EmbeddingModel == "fake@1");
        after.Select(r => (r.Id, r.IndexedAt)).ShouldBe(before.Select(r => (r.Id, r.IndexedAt)), ignoreOrder: true);
    }

    [Test]
    public async Task Index_WhenTextChanged_ShouldClearEmbedding()
    {
        var item = await ArrangeItemAsync("Old title", description: "Unchanged body.");
        await SyncAsync(item);
        await EmbedAllAsync(item);
        var before = await RowsAsync(item);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE work_item SET title = 'New title' WHERE id = @item", new { item });
        }

        await SyncAsync(item);

        var after = await RowsAsync(item);
        var title = after.Single(r => r.Field == "title");
        title.Text.ShouldBe("New title");
        title.TextHash.ShouldBe(SearchDocBuilder.HashOf("New title"));
        title.Embedded.ShouldBeFalse();
        title.EmbeddingModel.ShouldBeNull();
        title.Id.ShouldBe(before.Single(r => r.Field == "title").Id);
        // The body carries the title as its prefix, so it changed too.
        after.Single(r => r.Field == "body").Text.ShouldBe("New title\nUnchanged body.");
        after.Single(r => r.Field == "body").Embedded.ShouldBeFalse();
    }

    [Test]
    public async Task Index_WhenBodyShrinks_ShouldDropSurplusChunks()
    {
        var item = await ArrangeItemAsync("Long item", description: LongBody());
        await SyncAsync(item);
        var chunks = (await RowsAsync(item)).Where(r => r.Field == "body").ToList();
        chunks.Count.ShouldBeGreaterThanOrEqualTo(3);
        chunks.Select(r => r.ChunkNo).ShouldBe(Enumerable.Range(0, chunks.Count), ignoreOrder: true);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE work_item SET description = 'Now short.' WHERE id = @item", new { item });
        }

        await SyncAsync(item);

        var body = (await RowsAsync(item)).Where(r => r.Field == "body").ToList();
        body.Select(r => (r.ChunkNo, r.Text)).ShouldBe([(0, "Long item\nNow short.")]);
    }

    [Test]
    public async Task Index_WhenTagsAndBodyCleared_ShouldDropTheirRows()
    {
        var item = await ArrangeItemAsync("Emptied", tags: ["a"], description: "Something.");
        await SyncAsync(item);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE work_item SET tags = '{}', description = '' WHERE id = @item", new { item });
        }

        await SyncAsync(item);

        (await RowsAsync(item)).Select(r => r.Field).ShouldBe(["title"]);
    }

    [Test]
    public async Task Index_WhenItemRemoved_ShouldDropItsRows()
    {
        var item = await ArrangeItemAsync("Soon removed", tags: ["x"], description: "Body.");
        var comment = Guid.NewGuid();
        await using (var connection = await OpenDatabaseAsync())
        {
            await new SearchDocBuilder(Project, SearchSourceKind.Comment, comment, SearchField.Comment, "Soon removed\nA comment.").WithWorkItem(item).InsertAsync(connection);
        }
        await SyncAsync(item);
        (await RowsAsync(item)).Count.ShouldBe(3);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE work_item SET state_id = @removed WHERE id = @item", new { removed = SeedIds.States.Ship.StoryRemoved, item });
        }

        await SyncAsync(item);

        (await RowsAsync(item)).ShouldBeEmpty();
        // Comment rows of a removed item are kept (docs/search.md); the candidate joins hide them.
        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM search_doc WHERE source_id = @comment", new { comment })).ShouldBe(1);
    }

    [Test]
    public async Task Index_WhenItemSyncedTwice_ShouldKeepOneRowPerField()
    {
        var item = await ArrangeItemAsync("Twice", description: "Body.");

        await SyncAsync(item);
        await SyncAsync(item);

        (await RowsAsync(item)).Count.ShouldBe(2);
    }

    private async Task<Guid> ArrangeItemAsync(string title, string[]? tags = null, string description = "", string criteria = "[]", string repro = "")
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(Project).WithTitle(title).WithTags(tags ?? []).InsertAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE work_item SET description = @description, acceptance_criteria = @criteria::jsonb, repro_steps = @repro WHERE id = @item",
            new { item, description, criteria, repro });
        return item;
    }

    private async Task SyncAsync(Guid item)
    {
        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISearchDocWriter>().SyncAsync([new SearchSource(SearchSourceKind.Item, item)]);
    }

    private async Task EmbedAllAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE search_doc SET embedding = '{0.6,0.8}', embedding_model = 'fake@1' WHERE source_id = @item", new { item });
    }

    private async Task<List<Row>> RowsAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        return [.. await connection.QueryAsync<Row>(
            """
            SELECT id, source_kind AS sourceKind, source_id AS sourceId, work_item_id AS workItemId, project_id AS projectId, field,
                   chunk_no AS chunkNo, text, text_hash AS textHash, embedding IS NOT NULL AS embedded, embedding_model AS embeddingModel,
                   indexed_at AS indexedAt
            FROM search_doc WHERE source_kind = 'item' AND source_id = @item ORDER BY field, chunk_no
            """,
            new { item })];
    }

    private async Task<string> TsvAsync(Guid item, string field)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<string>("SELECT tsv::text FROM search_doc WHERE source_id = @item AND field = @field", new { item, field }) ?? "";
    }

    private static string LongBody() => string.Join("\n\n", Enumerable.Range(1, 6).Select(n => $"Paragraph {n}. " + new string('z', 480)));

    private sealed record Row(Guid Id, string SourceKind, Guid SourceId, Guid? WorkItemId, Guid ProjectId, string Field, int ChunkNo, string Text,
        string TextHash, bool Embedded, string? EmbeddingModel, DateTime IndexedAt);
}
