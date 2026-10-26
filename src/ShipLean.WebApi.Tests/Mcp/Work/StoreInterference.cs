using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Mcp.Work;

// What another person's write does between a tool reading an item and writing it: set Next, and the next PatchAsync
// through the store applies it first, so the tool's revision is stale. PatchCalls counts the tool's attempts.
public sealed class StoreInterference
{
    private WorkItemChanges? _next;
    private int _patchCalls;

    public int PatchCalls => _patchCalls;

    public void Before(WorkItemChanges next) => Interlocked.Exchange(ref _next, next);

    public WorkItemChanges? Take() => Interlocked.Exchange(ref _next, null);

    public void CountPatch() => Interlocked.Increment(ref _patchCalls);

    public void Reset()
    {
        Interlocked.Exchange(ref _next, null);
        Interlocked.Exchange(ref _patchCalls, 0);
    }
}
