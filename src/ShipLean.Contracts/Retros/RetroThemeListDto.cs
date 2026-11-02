namespace ShipLean.Contracts.Retros;

public sealed record RetroThemeListDto(RetroThemeMode Mode, IReadOnlyList<RetroThemeOptionDto> Themes);
