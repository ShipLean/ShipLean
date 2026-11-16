namespace ShipLean.Contracts.Inspection;

// Runs linked to one item, inside a time window. Every store query filters on the project and the window.
public sealed record RunQuery(string ProjectKey, string ItemKey, DateTimeOffset From, DateTimeOffset To, string? TenantOverride, string? SelectorOverride);
