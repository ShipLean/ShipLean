namespace ShipLean.Application.Features.Inspection;

// The instance's Inspection settings (K1). A plain record: reading them from SHIPLEAN_INSPECTION_* and rejecting bad values is
// not done here. Auth values are "none", "basic:<user>:<password>" or "bearer:<token>".
public sealed record InspectionOptions
{
    public bool Enabled { get; init; }

    // "clickhouse" or "tempo-loki"; required when Enabled.
    public string? Store { get; init; }

    public string? AgentOtlpEndpoint { get; init; }

    public string? AgentOtlpHeaders { get; init; }

    public string? ClickHouseConnection { get; init; }

    public string ClickHouseDatabase { get; init; } = "otel";

    public string ClickHouseTracesTable { get; init; } = "otel_traces";

    public string ClickHouseLogsTable { get; init; } = "otel_logs";

    public int RetentionDays { get; init; } = 30;

    public string? TempoUrl { get; init; }

    public string? TempoAuth { get; init; }

    public string? TempoTenant { get; init; }

    public string? LokiUrl { get; init; }

    public string? LokiAuth { get; init; }

    public string? LokiTenant { get; init; }

    public string LokiSelector { get; init; } = "{service_name=~\".+\"}";

    public InspectionFieldNames Fields { get; init; } = InspectionFieldNames.Defaults;

    public int MaxSearchDays { get; init; } = 30;

    // Contains a {traceId} placeholder; no link is shown when unset.
    public string? GrafanaUrlTemplate { get; init; }
}
