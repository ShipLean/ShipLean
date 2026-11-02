namespace ShipLean.Contracts.Retros;

public sealed record RetroFeaturesDto(IReadOnlyList<RetroFeatureRowDto> Features, IReadOnlyList<RetroFeatureRowDto> Loose);
