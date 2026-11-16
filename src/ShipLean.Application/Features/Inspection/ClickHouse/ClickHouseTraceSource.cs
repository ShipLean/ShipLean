using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

// Pure delegation: each port method goes to one part. Lane A fills the parts and gives each its own dependencies; this file does not change.
public sealed class ClickHouseTraceSource(
    ClickHouseRunFinder finder,
    ClickHouseRunReader reader,
    ClickHouseHealth health,
    ClickHouseFieldCheck fieldCheck) : ITraceSource
{
    public Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct) => finder.FindAsync(query, ct);

    public Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct) => reader.GetAsync(run, ct);

    public Task<StoreHealth> CheckAsync(CancellationToken ct) => health.CheckAsync(ct);

    public Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct) => fieldCheck.CheckAsync(ct);
}
