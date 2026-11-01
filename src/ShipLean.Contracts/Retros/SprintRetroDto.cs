using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record SprintRetroDto(SprintRef Sprint, RetroKpisDto Kpis, IReadOnlyList<SprintRef> Sprints, IReadOnlyList<RetroCategoryColumnDto> Categories, IReadOnlyList<RetroCardDto> Cards, IReadOnlyList<RetroCoverageRowDto> Coverage, IReadOnlyList<RetroActionRowDto> ActionItems, IReadOnlyList<RetroMentionStripDto> Recurring);
