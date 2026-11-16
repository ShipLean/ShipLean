namespace ShipLean.Contracts.Inspection;

public sealed record RunRef(string ProjectKey, string ItemKey, string RunId, DateTimeOffset From, DateTimeOffset To, string? TenantOverride, string? SelectorOverride);
