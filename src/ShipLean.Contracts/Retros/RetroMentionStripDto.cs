namespace ShipLean.Contracts.Retros;

public sealed record RetroMentionStripDto(RetroThemeRefDto Theme, IReadOnlyList<int> Counts, int? FixAt);
