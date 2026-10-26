using System.Data.Common;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.TestSupport.Contracts.WorkItems;

// Rules double for the contract fixtures: remembers every call, and optionally amends the change and returns notices.
public sealed class RecordingWorkItemRules(Func<ProposedChange, ProposedChange>? amend = null, IReadOnlyList<string>? notices = null) : IWorkItemRules
{
    private readonly List<RuleCall> _calls = [];

    public IReadOnlyList<RuleCall> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    public Task<RuleOutcome> ApplyAsync(WorkItemSnapshot? before, ProposedChange change, Guid? actorId, DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        lock (_calls)
        {
            _calls.Add(new RuleCall(before, change, actorId));
        }

        return Task.FromResult(new RuleOutcome(amend is null ? change : amend(change), notices ?? []));
    }
}
