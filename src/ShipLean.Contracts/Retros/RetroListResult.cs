namespace ShipLean.Contracts.Retros;

public sealed record RetroListResult(RetroThemeMode ThemeMode, IReadOnlyList<RetroPointDto> Points, IReadOnlyList<RetroThemeRefDto> Themes, bool Truncated);
