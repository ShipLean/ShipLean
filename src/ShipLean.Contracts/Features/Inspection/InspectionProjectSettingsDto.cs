namespace ShipLean.Contracts.Inspection;

// GET/PUT api/inspection/projects/{project}/settings. StoreMode is clickhouse or tempoLoki.
public sealed record InspectionProjectSettingsDto(bool Enabled, bool ShowContent, string? TenantOverride, string? SelectorOverride, string StoreMode);
