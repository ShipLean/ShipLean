namespace ShipLean.Contracts.Inspection;

// GET api/inspection/projects/{project}/items/{key}/runs/{runId}. Expired is true when the run left the store.
public sealed record RunPageDto(
    bool Expired,
    RunHeaderDto Header,
    bool LogOnly,
    string? Banner,
    bool Truncated,
    string? GrafanaUrl,
    bool ContentShown,
    IReadOnlyList<RunOverviewTurnDto> Overview,
    RunAxisDto Axis,
    IReadOnlyList<RunPageTurnDto> Turns);
