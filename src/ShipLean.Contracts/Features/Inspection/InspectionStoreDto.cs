namespace ShipLean.Contracts.Inspection;

// Mode is clickhouse or tempoLoki.
public sealed record InspectionStoreDto(string Mode, int RetentionDays);
