namespace ShipLean.Contracts.Persisted;

public sealed record LinkValue(LinkType Type, Guid? WorkItemId, string? Key, string? Url, string? Title);
