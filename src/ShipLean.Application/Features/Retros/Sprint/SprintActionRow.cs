namespace ShipLean.Application.Features.Retros.Sprint;

internal sealed record SprintActionRow(
    Guid ItemId, string Key, string Title, decimal? Points, Guid? ThemeId, string? ThemeName, string? ThemeTone,
    Guid StateId, string StateName, string StateCategory,
    Guid? PlannedId, string? PlannedName, string? PlannedStart, string? PlannedEnd,
    Guid? RaisedId, string? RaisedName, string? RaisedStart, string? RaisedEnd,
    Guid? OwnerId, string? OwnerName, string? OwnerInitials, string? OwnerColour, string? OwnerPhotoUrl,
    string? FixType, int PointCount);
