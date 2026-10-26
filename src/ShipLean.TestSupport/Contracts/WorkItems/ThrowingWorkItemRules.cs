using System.Data.Common;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.TestSupport.Contracts.WorkItems;

// Rules double that fails the test if the store calls it.
public sealed class ThrowingWorkItemRules : IWorkItemRules
{
    public Task<RuleOutcome> ApplyAsync(WorkItemSnapshot? before, ProposedChange change, Guid? actorId, DbTransaction? transaction, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The rules must not run here.");
}
