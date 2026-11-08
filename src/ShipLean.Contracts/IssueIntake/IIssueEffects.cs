using System.Data.Common;

namespace ShipLean.Contracts.IssueIntake;

// The effect ledger (github_issue_effect, D18). A step that creates something claims its key with the new id inside the
// create's own transaction: the claim commits or rolls back with the create, so a retried or concurrent step finds it.
public interface IIssueEffects
{
    // True when this call claimed the key; false when it was already claimed (a concurrent claim waits for the other
    // transaction and returns false if that one commits).
    Task<bool> TryClaimAsync(string key, Guid? issueId, Guid? result, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // The result a claim stored. Null when the key isn't claimed, or was claimed with no result.
    Task<Guid?> FindAsync(string key, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}
