namespace ShipLean.WebApi.Tests.Usage;

public sealed record UsageProbeResult(Guid? ProjectId, int ProjectCount, string? Viewport, IReadOnlyDictionary<string, object> Properties);
