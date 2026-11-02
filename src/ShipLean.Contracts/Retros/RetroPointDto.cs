using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroPointDto(Guid Id, Guid RetroId, RetroCategory Category, string Text, string? RootCause, RetroThemeRefDto? Theme, RetroThemeSetBy? ThemeSetBy, RetroAuthorDto Author, RetroItemRefDto? WorkItem, SprintRef? Sprint, bool IsFeature, bool Recurring, RetroActionRefDto? Action, bool CanArchive, RetroDecision? Decision, RetroNotNeededDto? NotNeeded);
