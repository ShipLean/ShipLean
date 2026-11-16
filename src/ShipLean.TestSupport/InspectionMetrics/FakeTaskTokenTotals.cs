using ShipLean.Contracts.Inspection;

namespace ShipLean.TestSupport.InspectionMetrics;

// An ITaskTokenTotals for tests that need no store (CK1). It answers every query with Totals, keeping only the asked keys, and
// records the queries it was asked, so a test can check what the view read.
public sealed class FakeTaskTokenTotals : ITaskTokenTotals
{
    private readonly List<TaskTokenQuery> queries = [];

    public TaskTokenTotals Totals { get; set; } = new(true, null, null, new Dictionary<string, TaskTokens>(StringComparer.Ordinal));

    public IReadOnlyList<TaskTokenQuery> Queries
    {
        get
        {
            lock (queries)
            {
                return [.. queries];
            }
        }
    }

    public Task<TaskTokenTotals> ReadAsync(TaskTokenQuery query, CancellationToken ct)
    {
        lock (queries)
        {
            queries.Add(query);
        }
        var asked = query.ItemKeys.ToHashSet(StringComparer.Ordinal);
        var byItem = Totals.ByItem.Where(pair => asked.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return Task.FromResult(Totals with { ByItem = byItem });
    }
}
