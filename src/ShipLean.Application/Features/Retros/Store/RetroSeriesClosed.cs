namespace ShipLean.Application.Features.Retros.Store;

// A closed action item on a theme and when it completed.
public sealed record RetroSeriesClosed(Guid ItemId, Guid ThemeId, DateTimeOffset CompletedAt);
