using System.Data.Common;

namespace ShipLean.Contracts.Agents;

// Short-lived URL tokens for moving a file between an agent's shell and an attachment (upload now, download later).
public interface ITransferTokens
{
    // How long a token lives.
    static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    Task<IssuedTransfer> IssueAsync(NewTransfer transfer, CancellationToken cancellationToken = default);

    // The same, inside the caller's transaction: the row is written on transaction.Connection and exists only if it commits.
    Task<IssuedTransfer> IssueAsync(NewTransfer transfer, DbTransaction transaction, CancellationToken cancellationToken = default);

    // Ends every token of that kind for that work item, spent or not. Revocation is terminal: Find and Claim refuse a revoked
    // token and Release cannot bring one back.
    Task RevokeAsync(TransferKind kind, Guid workItemId, CancellationToken cancellationToken = default);

    // The same, inside the caller's transaction: the revoke is undone if it rolls back.
    Task RevokeAsync(TransferKind kind, Guid workItemId, DbTransaction transaction, CancellationToken cancellationToken = default);

    // True when the token was revoked or is unknown (it can win nothing). Reads the row FOR SHARE, so it waits for an
    // uncommitted revoke of that row instead of reading around it.
    Task<bool> IsRevokedAsync(string token, DbTransaction transaction, CancellationToken cancellationToken = default);

    // Null when the token is unknown, of another kind, expired, spent, its agent authorization was revoked, or its user is
    // disabled or has changed security stamp. The caller does not say which.
    Task<TransferGrant?> FindAsync(TransferKind kind, string token, CancellationToken cancellationToken = default);

    // Atomically marks a live upload or demo token used before its body is read; false when it is unknown, expired or already claimed,
    // so of two concurrent uses only one wins.
    Task<bool> ClaimAsync(string token, CancellationToken cancellationToken = default);

    // Gives a claimed upload or demo token back (used_at cleared) after the upload failed, so the same URL can be retried.
    Task ReleaseAsync(string token, CancellationToken cancellationToken = default);
}
