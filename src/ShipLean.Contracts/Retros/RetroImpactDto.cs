namespace ShipLean.Contracts.Retros;

public sealed record RetroImpactDto(RetroActionRefDto Action, RetroThemeRefDto Theme, RetroVerdict Verdict, IReadOnlyList<int> Counts, int ClosedAt, double BeforeAvg, double AfterAvg, double? ChangePct, RetroPointDto? LatestMention);
