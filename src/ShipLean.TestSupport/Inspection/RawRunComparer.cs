using System.Globalization;
using ShipLean.Contracts.Inspection;

namespace ShipLean.TestSupport.Inspection;

// Compares the RawRun a store returned with a fixture's expected one (K3 normalisation). Times are compared as offsets from each
// run's own start, so a fixture rebased to "now" still matches. Only the attribute keys in compareKeys are compared; spans and
// logs are matched in time order. Numbers in attributes compare by value, so 42 and "42" and 42.0 are the same.
public static class RawRunComparer
{
    // What differs, one line each; empty when the runs are equal.
    public static IReadOnlyList<string> Differences(RawRun expected, RawRun actual, IReadOnlyCollection<string> compareKeys)
    {
        var diffs = new List<string>();
        var (e0, a0) = (expected.Summary.StartedAt, actual.Summary.StartedAt);
        Summary(expected.Summary, actual.Summary, diffs);
        Check(diffs, "ended", Rel(expected.Summary.EndedAt, e0), Rel(actual.Summary.EndedAt, a0));
        Check(diffs, "truncated", expected.Truncated, actual.Truncated);
        Check(diffs, "span count", expected.Spans.Count, actual.Spans.Count);
        foreach (var (e, a) in Order(expected.Spans, s => s.Start, s => s.SpanId).Zip(Order(actual.Spans, s => s.Start, s => s.SpanId)))
        {
            var id = $"span {e.SpanId}";
            Check(diffs, id + " id", e.SpanId, a.SpanId);
            Check(diffs, id + " trace", e.TraceId, a.TraceId);
            Check(diffs, id + " parent", e.ParentSpanId, a.ParentSpanId);
            Check(diffs, id + " name", e.Name, a.Name);
            Check(diffs, id + " kind", e.Kind, a.Kind);
            Check(diffs, id + " start", Rel(e.Start, e0), Rel(a.Start, a0));
            Check(diffs, id + " end", Rel(e.End, e0), Rel(a.End, a0));
            Check(diffs, id + " status", e.Status, a.Status);
            Check(diffs, id + " status message", e.StatusMessage, a.StatusMessage);
            Attributes(diffs, id, e.Attributes, a.Attributes, compareKeys);
            Check(diffs, id + " events", e.Events.Count, a.Events.Count);
        }
        Check(diffs, "log count", expected.Logs.Count, actual.Logs.Count);
        foreach (var (e, a) in Order(expected.Logs, l => l.Time, l => l.Body).Zip(Order(actual.Logs, l => l.Time, l => l.Body)))
        {
            var id = $"log {e.Body}@{Rel(e.Time, e0)}";
            Check(diffs, id + " time", Rel(e.Time, e0), Rel(a.Time, a0));
            Check(diffs, id + " severity", e.Severity, a.Severity);
            Check(diffs, id + " body", e.Body, a.Body);
            Check(diffs, id + " trace", e.TraceId, a.TraceId);
            Check(diffs, id + " span", e.SpanId, a.SpanId);
            Check(diffs, id + " session", e.SessionId, a.SessionId);
            Check(diffs, id + " prompt", e.PromptId, a.PromptId);
            Check(diffs, id + " event", e.EventName, a.EventName);
            Attributes(diffs, id, e.Attributes, a.Attributes, compareKeys);
        }
        return diffs;
    }

    private static void Summary(RunSummary e, RunSummary a, List<string> diffs)
    {
        Check(diffs, "run id", e.RunId, a.RunId);
        Check(diffs, "session id", e.SessionId, a.SessionId);
        Check(diffs, "agent", e.Agent, a.Agent);
        Check(diffs, "started by", e.StartedBy, a.StartedBy);
        Check(diffs, "models", string.Join('|', e.Models.Order(StringComparer.Ordinal)), string.Join('|', a.Models.Order(StringComparer.Ordinal)));
        Check(diffs, "summary span count", e.SpanCount, a.SpanCount);
        Check(diffs, "summary log count", e.LogCount, a.LogCount);
        Check(diffs, "error count", e.ErrorCount, a.ErrorCount);
        Check(diffs, "tokens in", e.TokensIn, a.TokensIn);
        Check(diffs, "tokens out", e.TokensOut, a.TokensOut);
        Check(diffs, "links", Links(e), Links(a));
    }

    private static string Links(RunSummary summary)
        => string.Join(',', summary.Links.OrderBy(l => l.ItemKey, StringComparer.Ordinal).Select(l => $"{l.ItemKey}:{l.Source}"));

    private static void Attributes(
        List<string> diffs, string id, IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual, IReadOnlyCollection<string> keys)
    {
        foreach (var key in keys)
        {
            var (hasE, hasA) = (expected.TryGetValue(key, out var e), actual.TryGetValue(key, out var a));
            if (hasE != hasA || (hasE && !SameValue(e!, a!)))
            {
                diffs.Add($"{id} attribute {key}: expected {(hasE ? e : "<absent>")}, got {(hasA ? a : "<absent>")}");
            }
        }
    }

    private static bool SameValue(string expected, string actual)
        => expected == actual
           || (decimal.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
               && decimal.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) && x == y);

    private static IEnumerable<T> Order<T>(IEnumerable<T> items, Func<T, DateTimeOffset> time, Func<T, string> tie)
        => items.OrderBy(time).ThenBy(tie, StringComparer.Ordinal);

    // Microseconds from the run's start.
    private static long Rel(DateTimeOffset time, DateTimeOffset start) => (time - start).Ticks / 10;

    private static void Check<T>(List<string> diffs, string what, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            diffs.Add($"{what}: expected {expected}, got {actual}");
        }
    }
}
