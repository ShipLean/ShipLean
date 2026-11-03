using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroFeatureRowDto(RetroItemRefDto Item, StateRef State, int Retros, int OwnRetros, RetroAggregateDto Totals, string? LastRetroLabel, bool NoFeatureRetro);
