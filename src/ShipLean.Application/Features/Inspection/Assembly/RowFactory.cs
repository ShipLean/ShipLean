using System.Globalization;
using ShipLean.Application.Features.Inspection.Linking;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Assembly;

internal static class RowFactory
{
    private static readonly string[] InputAttributes = ["tool_parameters", "tool_input", "input"];
    private static readonly string[] EditTools = ["Edit", "MultiEdit", "Write", "NotebookEdit", "apply_patch"];

    public static string LogRowId(int index) => $"log-{index + 1}";

    public static RunRow FromSpan(SpanRecord span, string? parentId, int depth, SpanRecord? wait, int collapsed, IReadOnlyList<LogRecordData> logs)
    {
        var toolName = span.Attributes.GetValueOrDefault("tool_name");
        var kind = KindOf(span, toolName);
        return new RunRow(
            span.SpanId, parentId, depth, kind, toolName ?? span.Name, span.Start, span.End, (long)(span.End - span.Start).TotalMilliseconds,
            span.Status == SpanStatus.Error, IsRetry(span.Attributes), wait?.Start, wait?.End, collapsed, span.Attributes,
            InputOf(span.Attributes), span.Attributes.GetValueOrDefault("output") ?? logs.Select(log => log.Attributes.GetValueOrDefault("output")).FirstOrDefault(o => o is not null),
            logs, FilePathOf(span.Attributes, toolName, null), span.Attributes.GetValueOrDefault("commit_sha"));
    }

    public static RunRow FromLog(LogRecordData log, string id)
    {
        var toolName = log.Attributes.GetValueOrDefault("tool_name");
        var kind = LinkRules.IsShipleanTool(toolName, log.Attributes.GetValueOrDefault("mcp_server_name")) ? RowKind.ShipleanTool : RowKind.Event;
        var durationMs = long.TryParse(log.Attributes.GetValueOrDefault("duration_ms"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) ? ms : (long?)null;
        var start = log.Time - TimeSpan.FromMilliseconds(durationMs ?? 0);
        return new RunRow(
            id, null, 0, kind, log.EventName ?? log.Body, start, durationMs is null ? null : log.Time, durationMs,
            log.Severity == "ERROR", false, null, null, 0, log.Attributes, InputOf(log.Attributes), log.Attributes.GetValueOrDefault("output"), [],
            FilePathOf(log.Attributes, toolName, log.EventName), log.Attributes.GetValueOrDefault("commit_sha"));
    }

    private static RowKind KindOf(SpanRecord span, string? toolName)
    {
        if (span.Name == SpanNames.Subagent)
        {
            return RowKind.Subagent;
        }
        if (span.Name == SpanNames.LlmRequest)
        {
            return RowKind.ModelCall;
        }
        return LinkRules.IsShipleanTool(toolName ?? span.Name, span.Attributes.GetValueOrDefault("mcp_server_name")) ? RowKind.ShipleanTool : RowKind.Tool;
    }

    private static bool IsRetry(IReadOnlyDictionary<string, string> attributes)
        => int.TryParse(attributes.GetValueOrDefault("attempt"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var attempt) && attempt > 1;

    private static string? InputOf(IReadOnlyDictionary<string, string> attributes)
        => InputAttributes.Select(name => attributes.GetValueOrDefault(name)).FirstOrDefault(value => value is not null);

    // The edited file: only edit tools and file-edit events carry one that means "edited" (a Read's path is not an edit).
    private static string? FilePathOf(IReadOnlyDictionary<string, string> attributes, string? toolName, string? eventName)
    {
        var isEdit = (toolName is not null && EditTools.Contains(toolName)) || (eventName?.EndsWith("FileEdit", StringComparison.Ordinal) ?? false);
        return isEdit ? attributes.GetValueOrDefault("file_path") : null;
    }
}
