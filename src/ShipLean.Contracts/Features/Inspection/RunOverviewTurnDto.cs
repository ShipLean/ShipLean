namespace ShipLean.Contracts.Inspection;

public sealed record RunOverviewTurnDto(int Turn, double StartPct, double EndPct, bool HasError, IReadOnlyList<RunWaitDto> Waits);
