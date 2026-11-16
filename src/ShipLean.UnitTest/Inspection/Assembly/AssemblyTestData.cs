using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Inspection.Assembly;

internal static class AssemblyTestData
{
    public static readonly DateTimeOffset Start = TestDates.Today;

    public static RunSummary Summary { get; } = new("s", "s", "claude-code", null, [], Start, Start.AddMinutes(1), 0, 0, 0, null, null, []);

    public static SpanRecord Span(string id, string? parent, string name, int fromSecond, int toSecond, params (string Key, string Value)[] attributes)
        => new("t", id, parent, name, "internal", Start.AddSeconds(fromSecond), Start.AddSeconds(toSecond), SpanStatus.Unset, null,
            attributes.ToDictionary(a => a.Key, a => a.Value), []);

    public static LogRecordData Log(int second, string eventName, string? spanId, string? promptId, params (string Key, string Value)[] attributes)
        => new(Start.AddSeconds(second), "INFO", eventName, attributes.ToDictionary(a => a.Key, a => a.Value), "t", spanId, "s", promptId, eventName);

    public static RawRun Run(IReadOnlyList<SpanRecord> spans, IReadOnlyList<LogRecordData> logs) => new(Summary, spans, logs, false);
}
