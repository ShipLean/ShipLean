namespace ShipLean.Contracts.Retros;

public sealed record RetroThemeGroupDto(RetroThemeRefDto? Theme, bool Recurring, IReadOnlyList<RetroPointDto> Points);
