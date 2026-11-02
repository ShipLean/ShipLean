using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroActionDetailDto(RetroActionRefDto Action, RetroThemeRefDto? Theme, IReadOnlyList<RetroPointDto> Points, SprintRef? RaisedIn);
