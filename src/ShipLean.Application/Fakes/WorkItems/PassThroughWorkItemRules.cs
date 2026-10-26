using System.Data.Common;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Fakes.WorkItems;

// Applies no rule: the change goes through unchanged, with no notices.
public sealed class PassThroughWorkItemRules : IWorkItemRules
{
    public Task<RuleOutcome> ApplyAsync(WorkItemSnapshot? before, ProposedChange change, Guid? actorId, DbTransaction? transaction, CancellationToken cancellationToken = default)
        => Task.FromResult(new RuleOutcome(change, []));
}
