using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// What one FindAsync call shares: the traces already fetched (so a trace named twice is read once), how many more fetches the call may
// make, and what was cut short, so the answer can say so instead of looking complete.
public sealed class FindScope
{
    // The most traces one call fetches from Tempo; cached reads do not count.
    public const int MaxTraces = 50;

    private readonly Dictionary<string, IReadOnlyList<SpanRecord>> traces = [];
    private readonly List<string> problems = [];
    private int fetched;

    public IReadOnlyList<string> Problems => problems;

    public void Note(string problem)
    {
        if (!problems.Contains(problem))
        {
            problems.Add(problem);
        }
    }

    // The trace's spans, or null when the call has used up its fetches (and says so in Problems).
    public async Task<IReadOnlyList<SpanRecord>?> GetAsync(string traceId, Func<Task<IReadOnlyList<SpanRecord>>> fetch)
    {
        if (traces.TryGetValue(traceId, out var spans))
        {
            return spans;
        }
        if (fetched >= MaxTraces)
        {
            Note($"Tempo returned more than {MaxTraces} traces for this item; only the first {MaxTraces} were read, so runs that needed more are left out.");
            return null;
        }
        fetched++;
        spans = await fetch();
        traces[traceId] = spans;
        return spans;
    }
}
