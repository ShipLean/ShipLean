namespace ShipLean.Contracts.Inspection;

// The run views behind K5: the section, the run page, the get_run rows page and one row's detail.
public interface IInspectionViews
{
    Task<AgentRunsSectionDto> SectionAsync(string projectKey, string itemKey, CancellationToken ct);

    Task<RunPageDto> RunAsync(string projectKey, string itemKey, string runId, RunFilter filter, CancellationToken ct);

    Task<RunRowDetailDto> RowAsync(string projectKey, string itemKey, string runId, string rowId, CancellationToken ct);

    Task<RunRowsPage> RunRowsAsync(string projectKey, string itemKey, string runId, bool errorsOnly, string? cursor, int? limit, CancellationToken ct);
}
