using System.Globalization;
using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Application.Features.Inspection.Settings;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Prompts;
using ShipLean.Core;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Views;

// The three run views behind K5, built from ITraceSource and IRunAssembler. Every RunQuery and RunRef comes from IInspectionItemLookup
// through RunWindow, and every label is formatted here with DisplayDate.
[RegisterPerRequest]
public sealed class InspectionViews(InspectionGate gate, IInspectionItemLookup items, ITraceSource source, IRunAssembler assembler, TimeProvider time, IViewerZone viewerZone, IPromptServingReads promptServings) : IInspectionViews
{
    // The section shows the newest runs; it assembles each one for its timeline, so it is capped.
    public const int MaxSectionRuns = 50;

    private const int MaxParallelReads = 4;

    private const int RowLineCap = 40;

    private static readonly TimeSpan PromptLeadIn = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["claude-code"] = "Claude Code",
        ["codex-cli"] = "Codex CLI",
        ["cursor"] = "Cursor",
    };

    // The service names agents send, mapped to the agent ids the screens and the setup tab use.
    private static readonly Dictionary<string, string> AgentIds = new(StringComparer.Ordinal) { ["codex_cli_rs"] = "codex-cli" };

    private static readonly Dictionary<string, string> Banners = new(StringComparer.Ordinal)
    {
        ["codex-cli"] = "Codex CLI sends events, not spans. Bars end at each event and reach back by its duration; events with no duration show as diamonds.",
        ["cursor"] = "Cursor sends hook events only: no model-call timing and no token counts. Events with no duration show as diamonds.",
    };

    public async Task<AgentRunsSectionDto> SectionAsync(string projectKey, string itemKey, CancellationToken ct)
    {
        var settings = await gate.RequireRunsAsync(projectKey, ct);
        var item = await items.RequireAsync(projectKey, itemKey, ct);
        var window = WindowOf(item);
        var found = await source.FindRunsAsync(new RunQuery(item.ProjectKey, item.ItemKey, window.From, window.To, settings.TenantOverride, settings.SelectorOverride), ct);
        var now = time.GetUtcNow();
        // The port reads one run per call, so the section reads its runs side by side (a few at a time) instead of one after the other.
        using var slots = new SemaphoreSlim(MaxParallelReads);
        var newest = found.Runs.OrderByDescending(r => r.StartedAt).Take(MaxSectionRuns).ToList();
        var assembledRuns = await Task.WhenAll(newest.Select(async summary =>
        {
            await slots.WaitAsync(ct);
            try
            {
                var raw = await source.GetRunAsync(new RunRef(item.ProjectKey, item.ItemKey, summary.RunId, window.From, window.To, settings.TenantOverride, settings.SelectorOverride), ct);
                return raw is null ? null : assembler.Assemble(raw);
            }
            finally
            {
                slots.Release();
            }
        }));
        var rows = new List<AgentRunRowDto>();
        var waits = 0L;
        for (var i = 0; i < newest.Count; i++)
        {
            var (summary, assembled) = (newest[i], assembledRuns[i]);
            var agent = AgentId(summary.Agent);
            waits += assembled is null ? 0 : WaitMs(assembled);
            rows.Add(new AgentRunRowDto(
                summary.RunId, agent, Label(agent), summary.StartedBy, DisplayDate.Relative(summary.StartedAt, now, viewerZone.Zone), assembled?.WhatItDid,
                summary.Links.First(l => l.ItemKey == item.ItemKey).Source, assembled?.Turns.Count ?? 0, summary.SpanCount + summary.LogCount,
                assembled?.LogOnly ?? summary.SpanCount == 0, summary.Models.FirstOrDefault(), (long)(summary.EndedAt - summary.StartedAt).TotalMilliseconds,
                summary.TokensIn, summary.TokensOut, summary.ErrorCount, assembled is null || assembled.LogOnly ? [] : Timeline(assembled, summary)));
        }
        var totals = new AgentRunsTotalsDto(rows.Count, rows.Sum(r => r.TookMs), SumOrNull(rows.Select(r => r.TokensIn)), SumOrNull(rows.Select(r => r.TokensOut)), rows.Sum(r => r.Errors), waits);
        return new AgentRunsSectionDto(found.Reachable, found.Problem, Store(), settings.ShowContent, totals, rows, $"/p/{projectKey}/settings/inspection");
    }

    public async Task<RunPageDto> RunAsync(string projectKey, string itemKey, string runId, RunFilter filter, CancellationToken ct)
    {
        var read = await ReadAsync(projectKey, itemKey, runId, ct);
        var (settings, raw) = (read.Settings, read.Run);
        if (raw is null)
        {
            return ExpiredPage(read.Expired!, itemKey, read.Runs);
        }
        var run = assembler.Assemble(raw);
        var summary = raw.Summary;
        var totalMs = DurationMs(summary.StartedAt, summary.EndedAt);
        long Ms(DateTimeOffset at) => (long)(at - summary.StartedAt).TotalMilliseconds;
        var agent = AgentId(summary.Agent);

        var older = read.Runs.LastOrDefault(r => r.StartedAt < summary.StartedAt);
        var newer = read.Runs.FirstOrDefault(r => r.StartedAt > summary.StartedAt);
        // D19: the prompts the run's items were served around its time; a serving can land just before the first span.
        var prompts = await promptServings.ForRunAsync(
            projectKey, [.. summary.Links.Select(l => l.ItemKey).Distinct(StringComparer.Ordinal)], agent, summary.StartedAt - PromptLeadIn, summary.EndedAt, ct);
        var header = new RunHeaderDto(
            agent, Label(agent), StartedLabel(summary.StartedAt), summary.StartedBy, summary.Links.First(l => l.ItemKey == itemKey).Source,
            older?.RunId, newer?.RunId, totalMs, run.Turns.Count, summary.SpanCount + summary.LogCount, summary.TokensIn, summary.TokensOut, summary.ErrorCount, WaitMs(run), prompts);

        var overview = run.Turns.Select(t => new RunOverviewTurnDto(
            t.Number, Pct(Ms(t.Start), totalMs), Pct(Ms(t.End), totalMs), t.Rows.Any(r => r.IsError),
            [.. t.Rows.Where(r => r.WaitStart is not null && r.WaitEnd is not null).Select(r => new RunWaitDto(Pct(Ms(r.WaitStart!.Value), totalMs), Pct(Ms(r.WaitEnd!.Value), totalMs)))])).ToList();

        var shown = run.Turns.AsEnumerable();
        var axis = new RunAxisDto(0, totalMs);
        if (filter.Turn is { } number)
        {
            var turn = run.Turns.FirstOrDefault(t => t.Number == number) ?? throw ApiException.Validation("There is no such turn in this run.", "turn");
            shown = [turn];
            axis = new RunAxisDto(Ms(turn.Start), Ms(turn.End));
        }
        var turns = shown.Select(t =>
        {
            IReadOnlyList<RunPageRowDto> rows = [.. t.Rows.Select(r => new RunPageRowDto(
                r.Id, r.ParentId, r.Depth, r.Kind, r.Name, Ms(r.Start), r.End is { } end ? Ms(end) : null, r.DurationMs, r.IsError, r.IsRetry,
                r.WaitStart is { } ws ? Ms(ws) : null, r.WaitEnd is { } we ? Ms(we) : null, r.CollapsedChildCount, r.Logs.Count > 0))];
            return new RunPageTurnDto(t.Number, $"Turn {t.Number}", settings.ShowContent ? t.PromptText : null, !settings.ShowContent && t.PromptText is not null,
                filter.ErrorsOnly ? RunRowFilter.ErrorsWithAncestors(rows) : rows);
        }).Where(t => !filter.ErrorsOnly || t.Rows.Count > 0).ToList();

        return new RunPageDto(false, header, run.LogOnly, run.LogOnly ? Banners.GetValueOrDefault(agent) : null, raw.Truncated, Grafana(raw.Spans.FirstOrDefault()?.TraceId),
            settings.ShowContent, overview, axis, turns);
    }

    // The rows of a run in one flat, errors-first order, one page at a time.
    public async Task<RunRowsPage> RunRowsAsync(string projectKey, string itemKey, string runId, bool errorsOnly, string? cursor, int? limit, CancellationToken ct)
    {
        if (limit is < 1 or > RunRowsCursor.MaxLimit)
        {
            throw ApiException.Validation($"limit must be 1 to {RunRowsCursor.MaxLimit}.", "limit");
        }
        var size = limit ?? RunRowsCursor.DefaultLimit;
        var filter = errorsOnly ? "errors" : "all";
        var position = RunRowsCursor.Decode(cursor, filter);
        var page = await RunAsync(projectKey, itemKey, runId, new RunFilter(errorsOnly, null), ct);
        var ordered = RunRowsCursor.Order(page.Turns.SelectMany(t => t.Rows.Select(r => new RunRowsPageRow(t.Number, r))));
        var rest = RunRowsCursor.After(ordered, position).ToList();
        var rows = rest.Take(size).ToList();
        var next = rows.Count < rest.Count ? RunRowsCursor.Encode(filter, rows[^1]) : null;
        return new RunRowsPage(page.Header, page.LogOnly, page.Banner, page.Truncated, page.GrafanaUrl, page.ContentShown, ordered.Count, ordered.Count(r => r.Row.IsError), next, rows);
    }

    public async Task<RunRowDetailDto> RowAsync(string projectKey, string itemKey, string runId, string rowId, CancellationToken ct)
    {
        var read = await ReadAsync(projectKey, itemKey, runId, ct);
        var (settings, raw) = (read.Settings, read.Run);
        if (raw is null)
        {
            throw ApiException.NotFound("Row not found: the run is no longer available.");
        }
        var row = assembler.Assemble(raw).Turns.SelectMany(t => t.Rows).FirstOrDefault(r => r.Id == rowId) ?? throw ApiException.NotFound("Row not found.");
        var shown = settings.ShowContent;
        var (input, _) = Capped(shown ? row.Input : null);
        var (output, outputTruncated) = Capped(shown ? row.Output : null);
        RunAttributeDto[] Attributes(IReadOnlyDictionary<string, string> source) => [.. ContentKeys.Visible(source, shown).Select(a => new RunAttributeDto(a.Key, a.Value))];
        return new RunRowDetailDto(
            row.Kind, row.Name, ClockSeconds(row.Start), row.DurationMs ?? 0, (long)(row.Start - raw.Summary.StartedAt).TotalMilliseconds,
            row.WaitStart is { } ws && row.WaitEnd is { } we ? (long)(we - ws).TotalMilliseconds : null, !shown, input, output, outputTruncated,
            Attributes(row.Attributes), [.. row.Logs.Select(l => new RunLogDto(ClockSeconds(l.Time), l.Severity, l.Body, Attributes(l.Attributes)))],
            row.FilePath, row.CommitSha, Grafana(raw.Spans.FirstOrDefault(s => s.SpanId == rowId)?.TraceId));
    }

    // Run is null for an expired run: listed for the item, but the store no longer returns its data. Expired then holds its listing.
    private async Task<(InspectionProjectStore.ProjectSettings Settings, RawRun? Run, RunSummary? Expired, IReadOnlyList<RunSummary> Runs)> ReadAsync(string projectKey, string itemKey, string runId, CancellationToken ct)
    {
        var settings = await gate.RequireRunsAsync(projectKey, ct);
        var item = await items.RequireAsync(projectKey, itemKey, ct);
        var window = WindowOf(item);
        var found = await source.FindRunsAsync(new RunQuery(item.ProjectKey, item.ItemKey, window.From, window.To, settings.TenantOverride, settings.SelectorOverride), ct);
        var run = await source.GetRunAsync(new RunRef(item.ProjectKey, item.ItemKey, runId, window.From, window.To, settings.TenantOverride, settings.SelectorOverride), ct);
        var ordered = found.Runs.OrderBy(r => r.StartedAt).ToList();
        if (run is not null)
        {
            return (settings, run, null, ordered);
        }
        // The store answers null for an expired or unknown run, and also when a read fails or times out; only a run past retention reads as expired.
        var listed = ordered.FirstOrDefault(r => r.RunId == runId);
        var all = ordered;
        if (listed is null && found.Reachable)
        {
            // The listing window stops at MaxSearchDays, but an expired run ended before retention: look again, wider, for this item only.
            var widest = time.GetUtcNow().AddDays(-(gate.Instance.RetentionDays + ClickHouseViewSql.ExpiredKeepDays));
            var from = item.CreatedAt > widest ? item.CreatedAt : widest;
            if (from < window.From)
            {
                var wider = await source.FindRunsAsync(new RunQuery(item.ProjectKey, item.ItemKey, from, window.To, settings.TenantOverride, settings.SelectorOverride), ct);
                listed = wider.Runs.FirstOrDefault(r => r.RunId == runId);
                found = wider.Reachable ? found : wider;
                if (listed is not null)
                {
                    all = wider.Runs.OrderBy(r => r.StartedAt).ToList();
                }
            }
        }
        var health = listed is null ? null : await source.CheckAsync(ct);
        if (!found.Reachable || health is { Reachable: false })
        {
            var problem = found.Reachable ? health?.Problem : found.Problem;
            var why = problem is null ? "" : ": " + problem;
            throw ApiException.Conflict($"The trace store is unreachable{why}. The run may still exist; try again shortly.");
        }
        if (listed is null)
        {
            throw ApiException.NotFound("Run not found.");
        }
        // Expired (D31): listed for the item and ended before the store's retention. A listed run that ended recently was a failed read.
        if (listed.EndedAt >= time.GetUtcNow().AddDays(-gate.Instance.RetentionDays))
        {
            throw ApiException.Conflict("The store didn't return this run. It may still exist; try again shortly.");
        }
        return (settings, null, listed, all);
    }

    private RunPageDto ExpiredPage(RunSummary summary, string itemKey, IReadOnlyList<RunSummary> runs)
    {
        var agent = AgentId(summary.Agent);
        var older = runs.LastOrDefault(r => r.StartedAt < summary.StartedAt);
        var newer = runs.FirstOrDefault(r => r.StartedAt > summary.StartedAt);
        var header = new RunHeaderDto(
            agent, Label(agent), StartedLabel(summary.StartedAt), summary.StartedBy, summary.Links.First(l => l.ItemKey == itemKey).Source,
            older?.RunId, newer?.RunId, DurationMs(summary.StartedAt, summary.EndedAt), 0, summary.SpanCount + summary.LogCount, summary.TokensIn, summary.TokensOut, summary.ErrorCount, 0, []);
        return new RunPageDto(true, header, false, null, false, null, false, [], new RunAxisDto(0, 0), []);
    }

    private (DateTimeOffset From, DateTimeOffset To) WindowOf(InspectionItemInfo item) => RunWindow.For(item.CreatedAt, time.GetUtcNow(), gate.Instance.MaxSearchDays);

    private InspectionStoreDto Store() => new(gate.Instance.Store == InspectionOptionsReader.ClickHouse ? "clickhouse" : "tempoLoki", gate.Instance.RetentionDays);

    private string? Grafana(string? traceId)
        => gate.Instance.GrafanaUrlTemplate is { } template && traceId is not null ? template.Replace("{traceId}", traceId, StringComparison.Ordinal) : null;

    private string StartedLabel(DateTimeOffset at) => $"{DisplayDate.ShortDay(at, time.GetUtcNow(), viewerZone.Zone)}, {DisplayDate.Time(at, viewerZone.Zone)}";

    private string ClockSeconds(DateTimeOffset at) => DisplayDate.LogTime(at, viewerZone.Zone);

    private static string AgentId(string service) => AgentIds.GetValueOrDefault(service, service);

    private static string Label(string agent) => Labels.GetValueOrDefault(agent, agent);

    private static long DurationMs(DateTimeOffset start, DateTimeOffset end) => (long)(end - start).TotalMilliseconds;

    private static double Pct(long ms, long totalMs) => totalMs <= 0 ? 0 : Math.Round(Math.Clamp(ms * 100.0 / totalMs, 0, 100), 1);

    private static long WaitMs(AssembledRun run) => run.Markers.Where(m => m.Kind == MarkerKind.Wait && m.Until is not null).Sum(m => (long)(m.Until!.Value - m.At).TotalMilliseconds);

    private static long? SumOrNull(IEnumerable<long?> values)
    {
        var present = values.Where(v => v is not null).ToList();
        return present.Count == 0 ? null : present.Sum();
    }

    // The strip: each row as its kind, its permission wait over it, and a failed row as an error. Log-only runs have no strip.
    private static List<TimelineSegmentDto> Timeline(AssembledRun run, RunSummary summary)
    {
        var total = DurationMs(summary.StartedAt, summary.EndedAt);
        var segments = new List<TimelineSegmentDto>();
        foreach (var row in run.Turns.SelectMany(t => t.Rows).Where(r => r.End is not null))
        {
            var start = Pct(DurationMs(summary.StartedAt, row.Start), total);
            var end = Pct(DurationMs(summary.StartedAt, row.End!.Value), total);
            var kind = row.IsError ? "error" : char.ToLowerInvariant(row.Kind.ToString()[0]) + row.Kind.ToString()[1..];
            segments.Add(new TimelineSegmentDto(kind, start, end));
            if (row.WaitStart is { } ws && row.WaitEnd is { } we)
            {
                segments.Add(new TimelineSegmentDto("wait", Pct(DurationMs(summary.StartedAt, ws), total), Pct(DurationMs(summary.StartedAt, we), total)));
            }
        }
        return segments;
    }

    // The first 40 lines, and whether there were more.
    private static (string? Text, bool Truncated) Capped(string? text)
    {
        if (text is null)
        {
            return (null, false);
        }
        var lines = text.Split('\n');
        return lines.Length <= RowLineCap ? (text, false) : (string.Join('\n', lines.Take(RowLineCap)), true);
    }
}
