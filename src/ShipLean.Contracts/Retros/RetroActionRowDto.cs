using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroActionRowDto(RetroActionRefDto Action, RetroThemeRefDto? Theme, SprintRef? RaisedIn, UserRef? Owner, RetroVerdict? Impact);
