namespace ShipLean.Contracts.Retros;

public sealed record RetroActionNew(string Title, RetroFixType FixType, IReadOnlyList<Guid> PointIds, Guid? ThemeId, string? ThemeName);
