using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// Delegates each port method to one part; lane B fills the parts.
public sealed class TempoLokiTraceSource : ITraceSource
{
    public Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct) => TempoLokiRunFinder.FindAsync(query, ct);

    public Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct) => TempoLokiRunReader.GetAsync(run, ct);

    public Task<StoreHealth> CheckAsync(CancellationToken ct) => TempoLokiHealth.CheckAsync(ct);

    public Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct) => TempoLokiFieldCheck.CheckAsync(ct);
}
