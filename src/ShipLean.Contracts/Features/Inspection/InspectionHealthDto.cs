namespace ShipLean.Contracts.Inspection;

// GET api/inspection/health (instance admin). Mode is off, clickhouse or tempoLoki.
public sealed record InspectionHealthDto(
    bool Enabled,
    string Mode,
    bool Reachable,
    int? LatencyMs,
    int? ViewLagSeconds,
    string? Problem,
    IReadOnlyList<InspectionFieldDto> Fields);
