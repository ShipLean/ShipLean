namespace ShipLean.Contracts.Retros;

public sealed record RetroLogResult(Guid RetroId, RetroItemRefDto Item, RetroRole Role, int PointsAdded, RetroThemeMode ThemeMode, IReadOnlyList<RetroThemeRefDto> Themes, IReadOnlyList<string> Warnings);
