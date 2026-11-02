namespace ShipLean.Contracts.Retros;

public sealed record RetroInsightColumnDto(string Key, string Label, RetroInsightColumnKind Kind, bool IsCurrent);
