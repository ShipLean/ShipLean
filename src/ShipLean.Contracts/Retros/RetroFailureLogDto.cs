namespace ShipLean.Contracts.Retros;

public sealed record RetroFailureLogDto(IReadOnlyList<string> Blockers, IReadOnlyList<string> AssumptionsDisproved, IReadOnlyList<string> Friction);
