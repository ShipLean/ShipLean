namespace ShipLean.Contracts.Retros;

public sealed record RetroThemeMerge(IReadOnlyList<Guid> SourceIds, Guid TargetId);
