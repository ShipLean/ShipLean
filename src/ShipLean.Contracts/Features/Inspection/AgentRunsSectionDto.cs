namespace ShipLean.Contracts.Inspection;

// GET api/inspection/projects/{project}/items/{key}/runs.
public sealed record AgentRunsSectionDto(
    bool StoreReachable,
    string? Problem,
    InspectionStoreDto Store,
    bool ContentShown,
    AgentRunsTotalsDto Totals,
    IReadOnlyList<AgentRunRowDto> Rows,
    string SetupUrl);
