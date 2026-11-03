using ShipLean.Contracts.Retros;

namespace ShipLean.Contracts.Retros.Decide;

public sealed record DecideCreateLine(string Title, RetroFixType FixType, string? Theme, int Covers, IReadOnlyList<DecideOpenAction> PossibleDuplicates);
