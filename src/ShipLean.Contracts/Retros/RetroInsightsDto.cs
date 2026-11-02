using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroInsightsDto(string Range, IReadOnlyList<SprintRef> Sprints, IReadOnlyList<RetroThemeInsightDto> Themes, IReadOnlyList<RetroThemeInsightDto> KeepsWorking, IReadOnlyList<RetroActionRowDto> ActionItems, IReadOnlyList<RetroImpactDto> Impact, RetroInsightsMode Mode, IReadOnlyList<RetroInsightColumnDto> Columns, RetroUnthemedDto Unthemed, IReadOnlyList<RetroMergeProposalDto> MergeProposals);
