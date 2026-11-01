namespace ShipLean.Contracts.Retros;

/// <summary>AfterAvg is null when Verdict is TooEarly. ChangePct is a fraction, (after - before) / before (e.g. -0.5), not a percentage.</summary>
public sealed record RetroImpactDto(RetroActionRefDto Action, RetroThemeRefDto Theme, RetroVerdict Verdict, IReadOnlyList<int> Counts, int ClosedAt, double BeforeAvg, double? AfterAvg, double? ChangePct, RetroPointDto? LatestMention);
