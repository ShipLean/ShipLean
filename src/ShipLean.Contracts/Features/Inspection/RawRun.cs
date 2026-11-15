namespace ShipLean.Contracts.Inspection;

public sealed record RawRun(RunSummary Summary, IReadOnlyList<SpanRecord> Spans, IReadOnlyList<LogRecordData> Logs, bool Truncated);
