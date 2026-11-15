using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Settings;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Views;

// The three run views behind K5, built from ITraceSource and IRunAssembler. Every RunQuery and RunRef comes from IInspectionItemLookup.
// TODO: totals, timeline, hiding of content, paging and the real turn layout (the views and their tests replace this).
[RegisterPerRequest]
public sealed class InspectionViews(InspectionGate gate, IInspectionItemLookup items, ITraceSource source, IRunAssembler assembler, TimeProvider time)
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["claude-code"] = "Claude Code",
        ["codex-cli"] = "Codex CLI",
        ["cursor"] = "Cursor",
    };

    public async Task<AgentRunsSectionDto> SectionAsync(string projectKey, string itemKey, CancellationToken ct)
    {
        var settings = await gate.RequireRunsAsync(projectKey, ct);
        var item = await items.RequireAsync(projectKey, itemKey, ct);
        var found = await source.FindRunsAsync(Query(settings, item), ct);
        var rows = found.Runs.Select(r => new AgentRunRowDto(
            r.RunId, r.Agent, Label(r.Agent), r.StartedBy, r.StartedAt.ToString("u"), null, r.Links.First(l => l.ItemKey == itemKey).Source, 0, r.SpanCount + r.LogCount,
            r.SpanCount == 0, r.Models.FirstOrDefault(), (long)(r.EndedAt - r.StartedAt).TotalMilliseconds, r.TokensIn, r.TokensOut, r.ErrorCount, [])).ToList();
        var totals = new AgentRunsTotalsDto(rows.Count, rows.Sum(r => r.TookMs), rows.Sum(r => r.TokensIn), rows.Sum(r => r.TokensOut), rows.Sum(r => r.Errors), 0);
        return new AgentRunsSectionDto(found.Reachable, found.Problem, Store(), settings.ShowContent, totals, rows, $"/p/{projectKey}/settings/inspection");
    }

    public async Task<RunPageDto> RunAsync(string projectKey, string itemKey, string runId, CancellationToken ct)
    {
        var (settings, raw) = await ReadAsync(projectKey, itemKey, runId, ct);
        var run = assembler.Assemble(raw);
        var start = raw.Summary.StartedAt;
        long Ms(DateTimeOffset at) => (long)(at - start).TotalMilliseconds;
        var header = new RunHeaderDto(
            raw.Summary.Agent, Label(raw.Summary.Agent), raw.Summary.StartedAt.ToString("u"), raw.Summary.StartedBy, raw.Summary.Links.First(l => l.ItemKey == itemKey).Source,
            null, null, Ms(raw.Summary.EndedAt), run.Turns.Count, raw.Summary.SpanCount + raw.Summary.LogCount, raw.Summary.TokensIn, raw.Summary.TokensOut, raw.Summary.ErrorCount, 0);
        var turns = run.Turns.Select(t => new RunPageTurnDto(
            t.Number, $"Turn {t.Number}", settings.ShowContent ? t.PromptText : null, !settings.ShowContent && t.PromptText is not null,
            [.. t.Rows.Select(r => new RunPageRowDto(
                r.Id, r.ParentId, r.Depth, r.Kind, r.Name, Ms(r.Start), r.End is { } end ? Ms(end) : null, r.DurationMs, r.IsError, r.IsRetry,
                r.WaitStart is { } ws ? Ms(ws) : null, r.WaitEnd is { } we ? Ms(we) : null, r.CollapsedChildCount, r.Logs.Count > 0))])).ToList();
        return new RunPageDto(false, header, run.LogOnly, null, raw.Truncated, Grafana(raw), settings.ShowContent, [], new RunAxisDto(0, Ms(raw.Summary.EndedAt)), turns);
    }

    public async Task<RunRowDetailDto> RowAsync(string projectKey, string itemKey, string runId, string rowId, CancellationToken ct)
    {
        var (settings, raw) = await ReadAsync(projectKey, itemKey, runId, ct);
        var row = assembler.Assemble(raw).Turns.SelectMany(t => t.Rows).FirstOrDefault(r => r.Id == rowId) ?? throw ApiException.NotFound("Row not found.");
        return new RunRowDetailDto(
            row.Kind, row.Name, row.Start.ToString("u"), row.DurationMs ?? 0, (long)(row.Start - raw.Summary.StartedAt).TotalMilliseconds, null, !settings.ShowContent,
            settings.ShowContent ? row.Input : null, settings.ShowContent ? row.Output : null, false,
            [.. row.Attributes.Select(a => new RunAttributeDto(a.Key, a.Value))],
            [.. row.Logs.Select(l => new RunLogDto(l.Time.ToString("u"), l.Severity, l.Body, [.. l.Attributes.Select(a => new RunAttributeDto(a.Key, a.Value))]))],
            row.FilePath, row.CommitSha, null);
    }

    private async Task<(Settings.InspectionProjectStore.ProjectSettings Settings, RawRun Run)> ReadAsync(string projectKey, string itemKey, string runId, CancellationToken ct)
    {
        var settings = await gate.RequireRunsAsync(projectKey, ct);
        var item = await items.RequireAsync(projectKey, itemKey, ct);
        var window = Window(item);
        var run = await source.GetRunAsync(new RunRef(projectKey, itemKey, runId, window.From, window.To, settings.TenantOverride, settings.SelectorOverride), ct);
        // TODO: expired runs answer 200 with expired=true once the store can tell an expired run from an unlinked one.
        return (settings, run ?? throw ApiException.NotFound("Run not found."));
    }

    private RunQuery Query(Settings.InspectionProjectStore.ProjectSettings settings, InspectionItemInfo item)
    {
        var window = Window(item);
        return new RunQuery(item.ProjectKey, item.ItemKey, window.From, window.To, settings.TenantOverride, settings.SelectorOverride);
    }

    private (DateTimeOffset From, DateTimeOffset To) Window(InspectionItemInfo item)
    {
        var now = time.GetUtcNow();
        var earliest = now.AddDays(-gate.Instance.MaxSearchDays);
        return (item.CreatedAt > earliest ? item.CreatedAt : earliest, now);
    }

    private InspectionStoreDto Store() => new(gate.Instance.Store == InspectionOptionsReader.ClickHouse ? "clickhouse" : "tempoLoki", gate.Instance.RetentionDays);

    private string? Grafana(RawRun raw)
        => gate.Instance.GrafanaUrlTemplate is { } template && raw.Spans.FirstOrDefault() is { } span ? template.Replace("{traceId}", span.TraceId, StringComparison.Ordinal) : null;

    private static string Label(string agent) => Labels.GetValueOrDefault(agent, agent);
}
