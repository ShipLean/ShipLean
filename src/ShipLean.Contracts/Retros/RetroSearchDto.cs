using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroSearchDto(IReadOnlyList<RetroPointDto> Rows, int Total, IReadOnlyList<SprintRef> Sprints, IReadOnlyList<RetroThemeRefDto> Themes, RetroCardDto? Selected);
