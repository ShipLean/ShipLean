namespace ShipLean.Contracts.Retros;

public sealed record RetroUnthemedDto(IReadOnlyList<int> Counts, int NoSprint, int Total);
