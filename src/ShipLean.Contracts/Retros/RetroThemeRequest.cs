namespace ShipLean.Contracts.Retros;

public sealed record RetroThemeRequest(Guid ProjectId, string Text, string? RootCause, string? SuppliedTheme, bool IsPerson);
