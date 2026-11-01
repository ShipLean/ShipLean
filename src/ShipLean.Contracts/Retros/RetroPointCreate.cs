namespace ShipLean.Contracts.Retros;

public sealed record RetroPointCreate(Guid? RetroId, Guid? WorkItemId, Guid? SprintId, RetroCategory Category, string Text, string? RootCause, Guid? ThemeId, string? ThemeName);
