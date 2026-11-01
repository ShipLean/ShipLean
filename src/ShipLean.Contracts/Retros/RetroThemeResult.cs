namespace ShipLean.Contracts.Retros;

public sealed record RetroThemeResult(Guid? ThemeId, RetroThemeSetBy? SetBy, float[]? Embedding, string? Warning);
