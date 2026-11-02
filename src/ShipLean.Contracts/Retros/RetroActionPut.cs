namespace ShipLean.Contracts.Retros;

public sealed record RetroActionPut(RetroFixType? FixType, Guid? ThemeId, string? ThemeName);
