namespace ShipLean.Contracts.Inspection;

// The only way ShipLean reads a telemetry store (K3). Implementations never throw for an unreachable store.
public interface ITraceSource
{
    // Runs linked to one item.
    Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct);

    // Null when the run is expired, not found, or not linked to RunRef.ItemKey.
    Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct);

    Task<StoreHealth> CheckAsync(CancellationToken ct);

    Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct);
}
