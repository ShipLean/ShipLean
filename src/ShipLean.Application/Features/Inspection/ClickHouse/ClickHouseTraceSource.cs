using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

// Delegates each port method to one part; lane A fills the parts.
public sealed class ClickHouseTraceSource : ITraceSource
{
    public Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct) => ClickHouseRunFinder.FindAsync(query, ct);

    public Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct) => ClickHouseRunReader.GetAsync(run, ct);

    public Task<StoreHealth> CheckAsync(CancellationToken ct) => ClickHouseHealth.CheckAsync(ct);

    public Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct) => ClickHouseFieldCheck.CheckAsync(ct);
}
