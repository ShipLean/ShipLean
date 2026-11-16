namespace ShipLean.Application.Features.Inspection.TempoLoki;

public sealed record RunLinkRow(string RunId, string SessionId, IReadOnlyList<string> TraceIds, string Source, string Summary, DateTimeOffset FetchedAt);
