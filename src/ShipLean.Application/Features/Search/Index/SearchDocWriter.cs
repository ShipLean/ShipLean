using System.Security.Cryptography;
using System.Text;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Search.Index.Chunking;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index;

// Writes search_doc from the text sources (docs/search.md). For each source asked for it builds the rows the source now
// produces (chunks, hashes), keeps the rows whose hash is unchanged, writes the rest with the embedding cleared, and deletes
// the rows the source no longer produces: a field it dropped, a chunk past the new last one, or a source it did not return at
// all (gone, removed or archived).
public sealed class SearchDocWriter(NpgsqlDataSource dataSource, IEnumerable<ISearchTextSource> textSources, SearchOptions options) : ISearchDocWriter
{
    private const int BatchSize = 200;

    public async Task SyncAsync(IReadOnlyList<SearchSource> sources, CancellationToken cancellationToken = default)
    {
        foreach (var kind in sources.GroupBy(s => s.Kind))
        {
            var ids = kind.Select(s => s.Id).Distinct().ToList();
            var textSource = SourceOf(kind.Key);
            foreach (var batch in ids.Chunk(BatchSize))
            {
                await SyncBatchAsync(kind.Key, textSource, batch, cancellationToken);
            }
        }
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var textSource in textSources)
        {
            // Rows written after this instant (an event sync of a source created mid-pass) are not in the snapshot below and must survive.
            DateTime started;
            await using (var clock = await dataSource.OpenConnectionAsync(cancellationToken))
            {
                started = await clock.ExecuteScalarAsync<DateTime>(new CommandDefinition("SELECT now()", cancellationToken: cancellationToken));
            }

            var ids = await textSource.AllIdsAsync(cancellationToken);
            foreach (var batch in ids.Chunk(BatchSize))
            {
                await SyncBatchAsync(textSource.Kind, textSource, batch, cancellationToken);
            }

            // Rows of sources that no longer exist at all are not reachable by id.
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var kindColumn = SearchDocColumns.Kind(textSource.Kind);
            await LockKindAsync(connection, transaction, kindColumn, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM search_doc WHERE source_kind = @kind AND source_id <> ALL(@ids) AND indexed_at < @started",
                new { kind = kindColumn, ids = ids.ToArray(), started }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private ISearchTextSource SourceOf(SearchSourceKind kind)
        => textSources.SingleOrDefault(s => s.Kind == kind) ?? throw new InvalidOperationException($"No search text source is registered for {kind}.");

    // Serialises writers of one kind so two transactions never take row locks in opposite orders (a full pass next to an event sync).
    private static Task LockKindAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string kind, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtext('search_doc:' || @kind))", new { kind }, transaction, cancellationToken: cancellationToken));

    private async Task SyncBatchAsync(SearchSourceKind kind, ISearchTextSource textSource, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var texts = await textSource.TextsAsync(ids, cancellationToken);
        foreach (var text in texts)
        {
            var repeated = text.Fields.GroupBy(f => f.Field).FirstOrDefault(g => g.Count() > 1);
            if (repeated is not null)
            {
                throw new InvalidOperationException($"The {kind} text source returned field {repeated.Key} more than once for source {text.SourceId}; a source text may carry each field once.");
            }
        }

        var wanted = texts.SelectMany(t => Rows(t)).ToList();
        var kindColumn = SearchDocColumns.Kind(kind);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await LockKindAsync(connection, transaction, kindColumn, cancellationToken);

        var existing = (await connection.QueryAsync<ExistingRow>(new CommandDefinition(
            """
            SELECT id, source_id AS sourceId, field, chunk_no AS chunkNo, text_hash AS textHash, project_id AS projectId, work_item_id AS workItemId
            FROM search_doc WHERE source_kind = @kind AND source_id = ANY(@ids)
            """,
            new { kind = kindColumn, ids = ids.ToArray() }, transaction, cancellationToken: cancellationToken))).ToList();

        var existingByKey = existing.ToDictionary(e => (e.SourceId, e.Field, e.ChunkNo));
        var wantedKeys = wanted.Select(w => (w.SourceId, w.Field, w.ChunkNo)).ToHashSet();

        var stale = existing.Where(e => !wantedKeys.Contains((e.SourceId, e.Field, e.ChunkNo)))
            .OrderBy(e => e.SourceId).ThenBy(e => e.Field, StringComparer.Ordinal).ThenBy(e => e.ChunkNo).Select(e => e.Id).ToArray();
        if (stale.Length > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM search_doc WHERE id = ANY(@ids)", new { ids = stale }, transaction, cancellationToken: cancellationToken));
        }

        var changed = wanted.Where(w => !existingByKey.TryGetValue((w.SourceId, w.Field, w.ChunkNo), out var e)
            || e.TextHash != w.TextHash || e.ProjectId != w.ProjectId || e.WorkItemId != w.WorkItemId)
            .OrderBy(w => w.SourceId).ThenBy(w => w.Field, StringComparer.Ordinal).ThenBy(w => w.ChunkNo).ToList();
        if (changed.Count > 0)
        {
            // A changed row keeps its id and loses its vector; indexed_at moves only for rows that changed.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO search_doc (id, project_id, source_kind, source_id, work_item_id, chunk_no, field, text, text_hash, indexed_at)
                SELECT gen_random_uuid(), p, @kind::text, s, w, c, f, t, h, now()
                FROM unnest(@projects::uuid[], @sources::uuid[], @items::uuid[], @chunks::int[], @fields::text[], @texts::text[], @hashes::text[]) AS u(p, s, w, c, f, t, h)
                ON CONFLICT (source_kind, source_id, field, chunk_no) DO UPDATE
                SET project_id = EXCLUDED.project_id, work_item_id = EXCLUDED.work_item_id, text = EXCLUDED.text,
                    text_hash = EXCLUDED.text_hash, embedding = NULL, embedding_model = NULL, indexed_at = EXCLUDED.indexed_at
                """,
                new
                {
                    kind = kindColumn,
                    projects = changed.Select(r => r.ProjectId).ToArray(),
                    sources = changed.Select(r => r.SourceId).ToArray(),
                    items = changed.Select(r => r.WorkItemId).ToArray(),
                    chunks = changed.Select(r => r.ChunkNo).ToArray(),
                    fields = changed.Select(r => r.Field).ToArray(),
                    texts = changed.Select(r => r.Text).ToArray(),
                    hashes = changed.Select(r => r.TextHash).ToArray(),
                },
                transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private IEnumerable<WantedRow> Rows(SearchSourceText source)
    {
        foreach (var field in source.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Text)))
        {
            var column = SearchDocColumns.Field(field.Field);
            var chunks = Chunks(field, source.ItemTitle);
            for (var no = 0; no < chunks.Count; no++)
            {
                yield return new WantedRow(source.SourceId, source.ProjectId, source.WorkItemId, column, no, chunks[no], HashOf(chunks[no]));
            }
        }
    }

    private IReadOnlyList<string> Chunks(SearchFieldText field, string? itemTitle)
    {
        if (field.Field is not (SearchField.Body or SearchField.Comment or SearchField.Attachment))
        {
            return [field.Text];
        }

        var prefix = string.IsNullOrWhiteSpace(itemTitle) ? null : itemTitle.Trim();
        return TextChunker.Chunk(field.Text, prefix, options.ChunkChars, options.ChunkOverlap);
    }

    private static string HashOf(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed record WantedRow(Guid SourceId, Guid ProjectId, Guid? WorkItemId, string Field, int ChunkNo, string Text, string TextHash);

    private sealed record ExistingRow(Guid Id, Guid SourceId, string Field, int ChunkNo, string TextHash, Guid ProjectId, Guid? WorkItemId);
}
