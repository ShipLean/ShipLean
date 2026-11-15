namespace ShipLean.Contracts.Inspection;

// Severity is the OTel severity text in upper case (INFO, ERROR), else derived from the number.
public sealed record LogRecordData(
    DateTimeOffset Time,
    string Severity,
    string Body,
    IReadOnlyDictionary<string, string> Attributes,
    string? TraceId,
    string? SpanId,
    string? SessionId,
    string? PromptId,
    string? EventName);
