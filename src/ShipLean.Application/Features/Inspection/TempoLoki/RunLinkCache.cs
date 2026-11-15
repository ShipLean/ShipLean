using System.Text.Json;
using System.Text.Json.Serialization;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// Tempo/Loki mode finds an item's runs by querying the stores. This keeps what it found for ten minutes in inspection_run_links
// (K2), so a page reload or a second MCP call does not query again. Only summaries are kept: no span or log data is copied.
[RegisterSingleton]
public sealed class RunLinkCache(IRunLinkStore store, TimeProvider time)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    // The runs found less than ten minutes ago for the item, or null when there are none that fresh (so the stores must be asked).
    public async Task<IReadOnlyList<RunSummary>?> GetAsync(string projectKey, string itemKey, CancellationToken ct)
    {
        var rows = await store.ReadAsync(projectKey, itemKey, time.GetUtcNow() - Lifetime, ct);
        return rows.Count == 0 ? null : [.. rows.Select(r => JsonSerializer.Deserialize<RunSummary>(r.Summary, Json)!)];
    }

    public Task SaveAsync(string projectKey, string itemKey, IReadOnlyList<RunSummary> runs, IReadOnlyDictionary<string, IReadOnlyList<string>> traceIds, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var rows = runs.Select(run => new RunLinkRow(
            run.RunId,
            run.SessionId,
            traceIds.TryGetValue(run.RunId, out var traces) ? traces : [],
            run.Links.First(l => l.ItemKey == itemKey).Source.ToString(),
            JsonSerializer.Serialize(run, Json),
            now)).ToList();
        return store.WriteAsync(projectKey, itemKey, rows, ct);
    }
}

public sealed record RunLinkRow(string RunId, string SessionId, IReadOnlyList<string> TraceIds, string Source, string Summary, DateTimeOffset FetchedAt);

// Where the cache rows live: inspection_run_links in ShipLean's own database.
public interface IRunLinkStore
{
    // Rows fetched after notBefore.
    Task<IReadOnlyList<RunLinkRow>> ReadAsync(string projectKey, string itemKey, DateTimeOffset notBefore, CancellationToken ct);

    // Upserts: an older row for the same run is replaced.
    Task WriteAsync(string projectKey, string itemKey, IReadOnlyList<RunLinkRow> rows, CancellationToken ct);
}

[RegisterSingleton]
public sealed class PostgresRunLinkStore(NpgsqlDataSource dataSource) : IRunLinkStore
{
    public async Task<IReadOnlyList<RunLinkRow>> ReadAsync(string projectKey, string itemKey, DateTimeOffset notBefore, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<StoredRow>(new CommandDefinition(
            """
            SELECT run_id AS runId, session_id AS sessionId, trace_ids AS traceIds, source, summary::text AS summary, fetched_at AS fetchedAt
            FROM inspection_run_links
            WHERE project_key = @projectKey AND item_key = @itemKey AND fetched_at > @notBefore
            """,
            new { projectKey, itemKey, notBefore }, cancellationToken: ct));
        return [.. rows.Select(r => new RunLinkRow(r.RunId, r.SessionId, r.TraceIds, r.Source, r.Summary, r.FetchedAt))];
    }

    public async Task WriteAsync(string projectKey, string itemKey, IReadOnlyList<RunLinkRow> rows, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO inspection_run_links (project_key, item_key, run_id, session_id, trace_ids, source, summary, fetched_at)
                VALUES (@projectKey, @itemKey, @runId, @sessionId, @traceIds, @source, @summary::jsonb, @fetchedAt)
                ON CONFLICT (project_key, item_key, run_id)
                DO UPDATE SET session_id = EXCLUDED.session_id, trace_ids = EXCLUDED.trace_ids, source = EXCLUDED.source,
                              summary = EXCLUDED.summary, fetched_at = EXCLUDED.fetched_at
                """,
                new { projectKey, itemKey, runId = row.RunId, sessionId = row.SessionId, traceIds = row.TraceIds.ToArray(), source = row.Source, summary = row.Summary, fetchedAt = row.FetchedAt },
                cancellationToken: ct));
        }
    }

    private sealed record StoredRow(string RunId, string SessionId, string[] TraceIds, string Source, string Summary, DateTimeOffset FetchedAt);
}
