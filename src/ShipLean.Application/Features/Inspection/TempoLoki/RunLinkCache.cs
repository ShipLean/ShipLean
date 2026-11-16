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
