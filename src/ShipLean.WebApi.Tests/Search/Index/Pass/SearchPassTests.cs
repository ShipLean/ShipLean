using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Search;
using ShipLean.Application.Features.Search.Index;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Search.Index.Pass;

[TestFixture]
public class SearchPassTests : PassFixture
{
    [Test]
    public async Task Pass_WhenRowsUnembedded_ShouldEmbedThem()
    {
        var goal = await ArrangeAsync("Login is fixed");
        var title = await ArrangeAsync("Slow checkout", SearchField.Title);
        var passages = Embedder.PassageTexts;

        var result = await RunPassAsync(false);

        result.ShouldBe(new IndexPassResult(0, 2, 0));
        Embedder.PassageTexts.ShouldBe(passages + 2);
        (await StoredAsync(goal)).ShouldBe(new StoredEmbedding(true, "fake-embedder@1"));
        (await StoredAsync(title)).ShouldBe(new StoredEmbedding(true, "fake-embedder@1"));
    }

    [Test]
    public async Task Pass_WhenRowIsLexicalOnly_ShouldLeaveItAlone()
    {
        var name = await ArrangeAsync("Sprint 12", SearchField.SprintName);
        var tags = await ArrangeAsync("payments urgent", SearchField.Tags);

        var result = await RunPassAsync(false);

        result.ShouldBe(new IndexPassResult(0, 0, 0));
        (await StoredAsync(name)).HasEmbedding.ShouldBeFalse();
        (await StoredAsync(tags)).HasEmbedding.ShouldBeFalse();
    }

    [Test]
    public async Task Index_WhenModelChanged_ShouldMarkAllStale()
    {
        var old = new EmbeddingModelId("fake-embedder", "1");
        var current = await ArrangeAsync("Current goal", embeddedBy: old);
        var other = await ArrangeAsync("Other goal", SearchField.Title, embeddedBy: old);
        var name = await ArrangeAsync("Sprint 13", SearchField.SprintName);
        (await RunPassAsync(false)).ShouldBe(new IndexPassResult(0, 0, 0));

        Embedder.ModelId = new EmbeddingModelId("fake-embedder", "2");
        var result = await RunPassAsync(false);

        result.ShouldBe(new IndexPassResult(0, 2, 0));
        (await StoredAsync(current)).Model.ShouldBe("fake-embedder@2");
        (await StoredAsync(other)).Model.ShouldBe("fake-embedder@2");
        (await StoredAsync(name)).ShouldBe(new StoredEmbedding(false, null));
    }

    [Test]
    public async Task Pass_WhenTwoRunConcurrently_ShouldEmbedEachRowOnce()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 90; i++)
        {
            ids.Add(await ArrangeAsync($"Goal number {i}"));
        }
        var passages = Embedder.PassageTexts;

        var results = await Task.WhenAll(RunPassAsync(false), RunPassAsync(false));

        Embedder.PassageTexts.ShouldBe(passages + 90);
        results.Sum(r => r.Embedded).ShouldBe(90);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM search_doc WHERE id = ANY(@ids) AND embedding IS NOT NULL", new { ids })).ShouldBe(90);
    }

    [Test]
    public async Task Pass_WhenMoreThanABatch_ShouldReportRemaining()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 70; i++)
        {
            ids.Add(await ArrangeAsync($"Batch goal {i}"));
        }
        var calls = Embedder.PassageCalls;

        // Another pass holds one row: this pass embeds everything else, in three batches of at most 32, and reports the one left.
        await using var holder = await OpenDatabaseAsync();
        await using var lockTransaction = await LockAsync(holder, ids[40]);
        var result = await RunPassAsync(false);

        result.ShouldBe(new IndexPassResult(0, 69, 1));
        (Embedder.PassageCalls - calls).ShouldBe(3);
        (await StoredAsync(ids[40])).HasEmbedding.ShouldBeFalse();

        await lockTransaction.CommitAsync();
        (await RunPassAsync(false)).ShouldBe(new IndexPassResult(0, 1, 0));
    }

    [Test]
    public async Task Pass_WhenTextChangesBetweenPasses_ShouldEmbedTheNewText()
    {
        var id = await ArrangeAsync("Before");
        await RunPassAsync(false);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE search_doc SET text = 'After', text_hash = @hash, embedding = NULL, embedding_model = NULL WHERE id = @id",
                new { id, hash = SearchDocBuilder.HashOf("After") });
        }

        (await RunPassAsync(false)).Embedded.ShouldBe(1);

        (await StoredAsync(id)).HasEmbedding.ShouldBeTrue();
    }

    [Test]
    public async Task Reindex_WhenFull_ShouldSyncAllFirst()
    {
        var dataSource = Host.Services.GetRequiredService<NpgsqlDataSource>();
        var writer = new InsertingSearchDocWriter(dataSource, Project, "Row written by the sync");
        var indexer = new SearchIndexer(dataSource, writer, Embedder, Host.Services.GetRequiredService<SearchOptions>());

        var result = await indexer.RunPassAsync(true);

        // The row did not exist before the sync, so embedding it in the same call means the sync ran first.
        result.Embedded.ShouldBe(1);
        result.Synced.ShouldBeGreaterThan(0);
        (await StoredAsync(writer.InsertedId)).ShouldBe(new StoredEmbedding(true, "fake-embedder@1"));
    }

    [Test]
    public async Task Reindex_WhenNotFull_ShouldNotSync()
    {
        await ArrangeAsync("Plain pass goal");
        var syncs = Writer.SyncAllCalls;

        var result = await RunPassAsync(false);

        result.ShouldBe(new IndexPassResult(0, 1, 0));
        Writer.SyncAllCalls.ShouldBe(syncs);
        (await RunPassAsync(true)).Synced.ShouldBeGreaterThan(0);
        Writer.SyncAllCalls.ShouldBe(syncs + 1);
    }

    [Test]
    public async Task Pass_WhenRowIsWrittenAfterItStarted_ShouldLeaveItForTheNextPass()
    {
        var early = await ArrangeAsync("Written before the pass");
        var late = await ArrangeAsync("Written after the pass started");
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE search_doc SET indexed_at = now() + interval '1 hour' WHERE id = @late", new { late });
        }

        var result = await RunPassAsync(false);

        result.ShouldBe(new IndexPassResult(0, 1, 1));
        (await StoredAsync(early)).HasEmbedding.ShouldBeTrue();
        (await StoredAsync(late)).HasEmbedding.ShouldBeFalse();

        // Its timestamp is in the future, so no later pass would claim it: remove it so other tests' counts stay exact.
        await using var cleanup = await OpenDatabaseAsync();
        await cleanup.ExecuteAsync("DELETE FROM search_doc WHERE id = @late", new { late });
    }
}
