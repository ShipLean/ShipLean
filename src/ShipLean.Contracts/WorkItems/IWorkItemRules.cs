using System.Data.Common;

namespace ShipLean.Contracts.WorkItems;

// The store calls this once per item, inside the write's transaction, from Create, Patch, PatchMany and Move, never from
// Import. Before is null on create. The real rules read states and board settings through the transaction (null in the fake store).
// Throws nothing of its own; the store passes ICurrentUser.Id as the actor, or null for a webhook write (no one to assign).
public interface IWorkItemRules
{
    Task<RuleOutcome> ApplyAsync(WorkItemSnapshot? before, ProposedChange change, Guid? actorId, DbTransaction? transaction, CancellationToken cancellationToken = default);
}
