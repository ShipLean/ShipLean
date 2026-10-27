namespace ShipLean.Contracts.Agents;

// Short-lived URL tokens for moving a file between an agent's shell and an attachment (upload now, download later).
public interface ITransferTokens
{
    // How long a token lives.
    static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    Task<IssuedTransfer> IssueAsync(NewTransfer transfer, CancellationToken cancellationToken = default);

    // Null when the token is unknown, of another kind, expired, spent, its agent authorization was revoked, or its user is
    // disabled or has changed security stamp. The caller does not say which.
    Task<TransferGrant?> FindAsync(TransferKind kind, string token, CancellationToken cancellationToken = default);

    // Atomically marks a live upload token used before its body is read; false when it is unknown, expired or already claimed,
    // so of two concurrent uses only one wins.
    Task<bool> ClaimAsync(string token, CancellationToken cancellationToken = default);

    // Gives a claimed upload token back (used_at cleared) after the upload failed, so the same URL can be retried.
    Task ReleaseAsync(string token, CancellationToken cancellationToken = default);
}
