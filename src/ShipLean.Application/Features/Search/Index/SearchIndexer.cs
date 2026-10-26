using Dapper;
using Npgsql;
using ShipLean.Application.Features.Search.Index.Pass;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index;

// One synchronous indexing pass (docs/search.md). A full pass first re-syncs every source through the writer. Then it embeds
// every embedded-field row that has no vector or whose vector came from another model, in batches, until none can be claimed.
// Remaining is what is still pending afterwards: rows another pass holds locked, which that pass embeds. The claim is bounded by the pass start, so rows written meanwhile wait for the next pass. A lexical embedder
// embeds nothing and reports nothing remaining, because there is nothing a pass could still do.
public sealed class SearchIndexer(NpgsqlDataSource dataSource, ISearchDocWriter writer, IEmbedder embedder, SearchOptions options) : ISearchIndexer
{
    public async Task<IndexPassResult> RunPassAsync(bool full, CancellationToken cancellationToken = default)
    {
        var synced = 0;
        if (full)
        {
            await writer.SyncAllAsync(cancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            synced = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT count(*)::int FROM (SELECT DISTINCT source_kind, source_id FROM search_doc) s", cancellationToken: cancellationToken));
        }

        if (embedder.Status.Mode != EmbedderMode.Semantic || embedder.ModelId is not { } model)
        {
            return new IndexPassResult(synced, 0, 0);
        }

        var modelId = model.ToString();
        DateTime passStarted;
        await using (var clock = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            passStarted = await clock.ExecuteScalarAsync<DateTime>(new CommandDefinition("SELECT now()", cancellationToken: cancellationToken));
        }

        var embedded = 0;
        while (true)
        {
            var count = await EmbedBatchAsync(modelId, passStarted, cancellationToken);
            if (count == 0)
            {
                break;
            }
            embedded += count;
        }

        await using var check = await dataSource.OpenConnectionAsync(cancellationToken);
        var remaining = await check.ExecuteScalarAsync<int>(new CommandDefinition(PendingRows.CountSql, PendingRows.Parameters(modelId, options.Batch), cancellationToken: cancellationToken));
        return new IndexPassResult(synced, embedded, remaining);
    }

    // Claims a batch with FOR UPDATE SKIP LOCKED and keeps the locks until the vectors are written, so a concurrent pass skips
    // these rows and the writer waits for them. The hash condition on the write is a second guard: a vector is only stored
    // for the text it was computed from.
    private async Task<int> EmbedBatchAsync(string modelId, DateTime passStarted, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var claimed = (await connection.QueryAsync<PendingRow>(new CommandDefinition(
            PendingRows.ClaimSql, PendingRows.Parameters(modelId, options.Batch, passStarted), transaction, cancellationToken: cancellationToken))).AsList();
        if (claimed.Count == 0)
        {
            return 0;
        }

        var vectors = await embedder.EmbedPassagesAsync(claimed.ConvertAll(r => r.Text), cancellationToken);
        // One statement for the batch: the vectors travel as one flat real[] and each row takes its slice by position.
        var dimensions = vectors[0].Length;
        var written = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE search_doc d
            SET embedding = (@flat::real[])[(c.ord - 1) * @dimensions + 1 : c.ord * @dimensions], embedding_model = @modelId, indexed_at = now()
            FROM unnest(@ids::uuid[], @hashes::text[]) WITH ORDINALITY AS c(id, hash, ord)
            WHERE d.id = c.id AND d.text_hash = c.hash
            """,
            new
            {
                flat = vectors.SelectMany(v => v).ToArray(),
                dimensions,
                modelId,
                ids = claimed.ConvertAll(r => r.Id).ToArray(),
                hashes = claimed.ConvertAll(r => r.TextHash).ToArray(),
            },
            transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return written;
    }
}
