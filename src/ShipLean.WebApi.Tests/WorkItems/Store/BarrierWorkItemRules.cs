using System.Data.Common;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// Rules double that holds each write inside its transaction until the other write has reached the rules as well, so both
// writes have taken their locks and read their peers before either commits. A store that serialises its writers never
// lets the second write in, so the first gives up waiting after the timeout and goes on; a later arrival does not wait.
public sealed class BarrierWorkItemRules(TimeSpan timeout) : IWorkItemRules
{
    private readonly TaskCompletionSource _bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public Task<RuleOutcome> ApplyAsync(WorkItemSnapshot? before, ProposedChange change, Guid? actorId, DbTransaction? transaction, CancellationToken cancellationToken = default)
        => ArriveAsync(change);

    private async Task<RuleOutcome> ArriveAsync(ProposedChange change)
    {
        if (Interlocked.Increment(ref _arrived) >= 2)
        {
            _bothArrived.TrySetResult();
        }
        else
        {
            await Task.WhenAny(_bothArrived.Task, Task.Delay(timeout));
        }

        return new RuleOutcome(change, []);
    }
}
