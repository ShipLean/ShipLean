namespace ShipLean.Contracts.Retros;

public sealed record RetroCategoryColumnDto(RetroCategory Category, IReadOnlyList<RetroThemeGroupDto> Groups);
