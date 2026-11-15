using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// A trace source that remembers what the views asked it, and answers from another source.
public sealed class RecordingTraceSource(ITraceSource inner) : ITraceSource
{
    private readonly List<RunQuery> _queries = [];
    private readonly List<RunRef> _refs = [];

    public IReadOnlyList<RunQuery> Queries => _queries;

    public IReadOnlyList<RunRef> Refs => _refs;

    public void Clear()
    {
        _queries.Clear();
        _refs.Clear();
    }

    public Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct)
    {
        _queries.Add(query);
        return inner.FindRunsAsync(query, ct);
    }

    public Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct)
    {
        _refs.Add(run);
        return inner.GetRunAsync(run, ct);
    }

    public Task<StoreHealth> CheckAsync(CancellationToken ct) => inner.CheckAsync(ct);

    public Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct) => inner.CheckFieldsAsync(ct);
}
