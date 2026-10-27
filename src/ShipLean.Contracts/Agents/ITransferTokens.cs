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

    // Marks an upload token used; call it once the attachment is stored. Idempotent.
    Task SpendAsync(string token, CancellationToken cancellationToken = default);
}
