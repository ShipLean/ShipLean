using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection;

// The ITraceSource while SHIPLEAN_INSPECTION is off. No route reaches it (they answer not_found first); it exists so the port always resolves.
public sealed class DisabledTraceSource : ITraceSource
{
    private const string Off = "Inspection is off (SHIPLEAN_INSPECTION).";

    public Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct) => Task.FromResult(new RunSearch([], false, Off));

    public Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct) => Task.FromResult<RawRun?>(null);

    public Task<StoreHealth> CheckAsync(CancellationToken ct) => Task.FromResult(new StoreHealth(false, null, null, Off));

    public Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FieldCheck>>([]);
}
