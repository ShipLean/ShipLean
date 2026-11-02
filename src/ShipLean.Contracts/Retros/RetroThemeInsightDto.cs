namespace ShipLean.Contracts.Retros;

public sealed record RetroThemeInsightDto(RetroThemeRefDto Theme, IReadOnlyList<int> Counts, int Total, string? Latest, RetroThemeStatus Status, IReadOnlyList<RetroActionRefDto> ActionItems, RetroVerdict? LastVerdict);
