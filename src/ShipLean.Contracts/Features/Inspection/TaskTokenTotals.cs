namespace ShipLean.Contracts.Inspection;

// The tokens per task ITaskTokenTotals found. A task with no counted run is absent from ByItem, never zero. KeptSince is the oldest
// instant the store still keeps runs for (null when it does not say); a period starting before it has no numbers. Available is
// false, with a Problem and no items, when the store is off, unreachable or cannot total tokens per task.
public sealed record TaskTokenTotals(bool Available, string? Problem, DateTimeOffset? KeptSince, IReadOnlyDictionary<string, TaskTokens> ByItem)
{
    public static TaskTokenTotals Unavailable(string problem) => new(false, problem, null, new Dictionary<string, TaskTokens>(StringComparer.Ordinal));
}
