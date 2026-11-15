namespace ShipLean.Application.Features.Inspection.TempoLoki;

// Where the cache rows live: inspection_run_links in ShipLean's own database.
public interface IRunLinkStore
{
    // Rows fetched after notBefore.
    Task<IReadOnlyList<RunLinkRow>> ReadAsync(string projectKey, string itemKey, DateTimeOffset notBefore, CancellationToken ct);

    // Upserts: an older row for the same run is replaced.
    Task WriteAsync(string projectKey, string itemKey, IReadOnlyList<RunLinkRow> rows, CancellationToken ct);
}
