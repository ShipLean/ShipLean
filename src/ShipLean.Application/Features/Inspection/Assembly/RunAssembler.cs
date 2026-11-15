using System.Globalization;
using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Linking;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Assembly;

// K4: raw spans and logs into turns, rows and markers. Pure; the rules are pinned by Fixtures/README.md.
[RegisterSingleton]
public sealed class RunAssembler : IRunAssembler
{
    public AssembledRun Assemble(RawRun raw)
    {
        raw = Deduplicated(raw);
        var logOnly = raw.Spans.Count == 0;
        var turns = logOnly ? LogOnlyTurns(raw.Logs) : TracedTurns(raw);
        var rows = turns.SelectMany(turn => turn.Rows).ToList();
        // A log-only event happened at its record time (the row end); a span starts the call.
        // What an error marker says: the span's status message, or the log record's body in a log-only run.
        var errorLabels = logOnly
            ? raw.Logs.OrderBy(log => log.Time).Select((log, index) => (Id: RowFactory.LogRowId(index), log.Body)).ToDictionary(item => item.Id, item => item.Body)
            : raw.Spans.Where(span => span.StatusMessage is not null).ToDictionary(span => span.SpanId, span => span.StatusMessage!);
        return new AssembledRun(raw.Summary, logOnly, turns, Markers(rows, errorLabels, logOnly), WhatItDid.From(rows));
    }

    // A collector retry can export a span or log twice and the stores do not dedupe, so each span id and each identical log counts once.
    // The first copy by start (then end) wins, which keeps the result independent of input order.
    private static RawRun Deduplicated(RawRun raw)
    {
        var spans = raw.Spans.GroupBy(span => span.SpanId)
            .Select(group => group.OrderBy(span => span.Start).ThenBy(span => span.End).First())
            .OrderBy(span => span.Start).ThenBy(span => span.End).ThenBy(span => span.SpanId, StringComparer.Ordinal)
            .ToList();
        var logs = raw.Logs.GroupBy(LogKey).Select(group => group.First()).ToList();
        return raw with { Spans = spans, Logs = logs };
    }

    private static string LogKey(LogRecordData log)
        => string.Join('\u001f', log.Time.UtcTicks, log.Severity, log.EventName, log.Body, log.SpanId, log.PromptId,
            string.Join('\u001e', log.Attributes.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => a.Key + "=" + a.Value)));

    private static List<Turn> TracedTurns(RawRun raw)
    {
        var children = raw.Spans.Where(span => span.ParentSpanId is not null).ToLookup(span => span.ParentSpanId!);
        var inInteraction = new HashSet<string>();
        var turns = new List<(Turn Turn, DateTimeOffset Sort)>();

        foreach (var interaction in raw.Spans.Where(span => span.Name == SpanNames.Interaction))
        {
            var rows = RowsUnder(children[interaction.SpanId], raw.Logs, children, null, 0, inInteraction);
            var promptId = interaction.Attributes.GetValueOrDefault(InspectionFieldNames.Defaults.Prompt);
            turns.Add((new Turn(0, interaction.Start, interaction.End, promptId, LogJoin.PromptText(raw.Logs, interaction, promptId), rows), interaction.Start));
        }
        var attached = raw.Logs.Where(log => log.SpanId is not null && raw.Spans.Any(span => span.SpanId == log.SpanId)).ToList();

        // Spans outside any interaction span group by their prompt id, each top-level span being one whose parent is not in the run.
        var orphans = raw.Spans.Where(span => span.Name != SpanNames.Interaction && !inInteraction.Contains(span.SpanId)).ToList();
        var orphanIds = orphans.Select(span => span.SpanId).ToHashSet();
        foreach (var group in orphans.Where(span => span.ParentSpanId is null || !orphanIds.Contains(span.ParentSpanId)).GroupBy(span => span.Attributes.GetValueOrDefault(InspectionFieldNames.Defaults.Prompt)))
        {
            var rows = RowsUnder(group, raw.Logs, children, null, 0, inInteraction);
            // A span whose parent is missing but whose prompt id matches an interaction's turn joins that turn as top-level rows.
            var home = group.Key is null ? -1 : turns.FindIndex(t => t.Turn.PromptId == group.Key);
            if (rows.Count > 0 && home >= 0)
            {
                var turn = turns[home].Turn;
                turns[home] = (turn with { Rows = [.. turn.Rows, .. rows], End = Max(turn.End, rows.Max(row => row.End ?? row.Start)) }, turns[home].Sort);
            }
            else if (rows.Count > 0)
            {
                turns.Add((new Turn(0, rows.Min(row => row.Start), rows.Max(row => row.End ?? row.Start), group.Key, null, rows), rows.Min(row => row.Start)));
            }
        }
        AddUnjoinedLogs(turns, raw, attached);
        return Number(turns.OrderBy(t => t.Sort).Select(t => t.Turn));
    }

    // Logs from services that are not the agent (a test runner) carry the run's session or item but no span of the run. A log that
    // matches no row and joins no interaction becomes a top-level Event row in the turn whose time window holds it; outside every turn it is not shown.
    private static void AddUnjoinedLogs(List<(Turn Turn, DateTimeOffset Sort)> turns, RawRun raw, List<LogRecordData> attached)
    {
        var interactions = raw.Spans.Where(span => span.Name == SpanNames.Interaction).ToList();
        var unjoined = raw.Logs
            .Where(log => !attached.Contains(log) && !interactions.Any(i => LogJoin.JoinsInteraction(log, i, i.Attributes.GetValueOrDefault(InspectionFieldNames.Defaults.Prompt))))
            .OrderBy(log => log.Time).ToList();
        for (var index = 0; index < unjoined.Count; index++)
        {
            var log = unjoined[index];
            var home = turns.FindIndex(t => log.Time >= t.Turn.Start && log.Time <= t.Turn.End);
            if (home >= 0)
            {
                var turn = turns[home].Turn;
                turns[home] = (turn with { Rows = [.. turn.Rows, RowFactory.FromLog(log, RowFactory.LogRowId(index))] }, turns[home].Sort);
            }
        }
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private static List<RunRow> RowsUnder(
        IEnumerable<SpanRecord> level, IReadOnlyList<LogRecordData> logs, ILookup<string, SpanRecord> children, string? rowParentId, int depth, HashSet<string> seen)
    {
        var rows = new List<RunRow>();
        foreach (var span in level.OrderBy(s => s.Start))
        {
            seen.Add(span.SpanId);
            var kids = children[span.SpanId].OrderBy(s => s.Start).ToList();
            if (span.Name is SpanNames.BlockedOnUser or SpanNames.Execution)
            {
                MarkSeen(span.SpanId, children, seen);
                continue;
            }
            var subagent = span.Name == SpanNames.Subagent;
            var wait = subagent ? null : kids.FirstOrDefault(kid => kid.Name == SpanNames.BlockedOnUser);
            var collapsed = subagent ? CountDescendants(span.SpanId, children) : 0;
            var rowLogs = logs.Where(log => log.SpanId == span.SpanId).OrderBy(log => log.Time).ToList();
            rows.Add(RowFactory.FromSpan(span, rowParentId, depth, wait, collapsed, rowLogs));
            if (subagent)
            {
                MarkSeen(span.SpanId, children, seen);
            }
            else
            {
                rows.AddRange(RowsUnder(kids, logs, children, span.SpanId, depth + 1, seen));
            }
        }
        return rows;
    }

    private static int CountDescendants(string spanId, ILookup<string, SpanRecord> children)
        => children[spanId].Sum(child => 1 + CountDescendants(child.SpanId, children));

    private static void MarkSeen(string spanId, ILookup<string, SpanRecord> children, HashSet<string> seen)
    {
        foreach (var childId in children[spanId].Select(child => child.SpanId))
        {
            seen.Add(childId);
            MarkSeen(childId, children, seen);
        }
    }

    private static List<Turn> LogOnlyTurns(IReadOnlyList<LogRecordData> logs)
    {
        var rows = logs.OrderBy(log => log.Time).Select((log, index) => (Log: log, Row: RowFactory.FromLog(log, RowFactory.LogRowId(index)))).ToList();
        var turns = rows
            .GroupBy(item => item.Log.PromptId)
            .Select(group =>
            {
                var turnRows = group.Select(item => item.Row).ToList();
                var prompt = group.Select(item => item.Log).FirstOrDefault(LogJoin.IsUserPrompt);
                return (Turn: new Turn(0, turnRows.Min(row => row.Start), turnRows.Max(row => row.End ?? row.Start), group.Key, prompt?.Attributes.GetValueOrDefault("prompt"), turnRows), Sort: turnRows.Min(row => row.Start));
            });
        return Number(turns.OrderBy(t => t.Sort).Select(t => t.Turn));
    }

    private static List<Turn> Number(IEnumerable<Turn> turns) => [.. turns.Select((turn, index) => turn with { Number = index + 1 })];

    private static List<RunMarker> Markers(List<RunRow> rows, Dictionary<string, string> errorLabels, bool logOnly)
    {
        var markers = new List<RunMarker>();
        foreach (var row in rows)
        {
            if (row is { WaitStart: { } waitStart, WaitEnd: { } waitEnd })
            {
                markers.Add(new RunMarker(MarkerKind.Wait, waitStart, waitEnd, row.Name));
            }
            if (row.IsError)
            {
                markers.Add(new RunMarker(MarkerKind.Error, row.End ?? row.Start, null, errorLabels.GetValueOrDefault(row.Id) ?? row.Name));
            }
            else if (row.Logs.FirstOrDefault(log => log.Severity == "ERROR") is { } failed)
            {
                markers.Add(new RunMarker(MarkerKind.Error, failed.Time, null, failed.Body));
            }
            if (row.Kind == RowKind.ShipleanTool)
            {
                markers.Add(new RunMarker(MarkerKind.ShipleanTool, logOnly ? row.End ?? row.Start : row.Start, null, WhatItDid.ShortName(row.Attributes.GetValueOrDefault("tool_name") ?? row.Name)));
            }
        }
        return [.. markers.OrderBy(marker => marker.At)];
    }
}
