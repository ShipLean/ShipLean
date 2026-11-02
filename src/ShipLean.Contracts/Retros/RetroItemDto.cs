using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroItemDto(RetroItemRefDto Item, StateRef State, RetroItemRefDto? Parent, RetroAggregateDto Totals, IReadOnlyList<RetroCategoryColumnDto> Categories, IReadOnlyList<RetroCardDto> Cards, IReadOnlyList<RetroActionRefDto> Actions, IReadOnlyList<RetroFeatureRowDto> Children);
