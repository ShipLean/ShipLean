namespace ShipLean.Application.Features.Retros.Insights;

internal sealed record InsightSprintRow(Guid Id, string Name, string StartDate, string EndDate);

internal sealed record InsightPointRow(Guid Id, Guid RetroId, Guid ThemeId, Guid SprintId, string Category, DateTime CreatedAt);

internal sealed record InsightThemeRow(Guid Id, string Name, string Tone);

internal sealed record InsightActionRow(
    Guid WorkItemId, Guid? ThemeId, Guid? RaisedSprintId, string Key, string Title, decimal? Points, DateTime CreatedAt,
    Guid StateId, string StateName, string StateCategory, Guid? SprintId,
    Guid? OwnerId, string? OwnerName, string? OwnerInitials, string? OwnerColour, string? OwnerPhotoUrl);
