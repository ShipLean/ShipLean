namespace ShipLean.Contracts.DeviceSessions;

// The phone app's sign-ins: a single-use code made on the confirm page (cookie session), exchanged by the app for a
// device session and its bearer secret. Only hashes of codes and secrets are stored. Every method takes `now` and
// never reads a clock. Pinned rules (DeviceSessionsContract):
// - CreateCodeAsync stores the user's security stamp as it was when they confirmed; the exchange copies it into the
//   session, so a stamp change inside the code's two minutes gives a session that is already ended.
// - ExchangeAsync: single use, valid for DeviceSessionLimits.CodeLifetime, S256 check of the verifier against the
//   challenge. A wrong verifier still uses up the code. Of two concurrent exchanges of one code exactly one wins.
//   Null for a bad, used or expired code or a wrong verifier.
// - FindBySecretAsync returns the session whatever its state (revoked and expired included); the caller decides.
// - TouchAsync slides ExpiresAt to now + DeviceSessionLimits.SessionLifetime and sets LastUsedAt, at most one write per
//   DeviceSessionLimits.TouchInterval per session.
// - ListAsync: the user's sessions that are neither revoked nor expired at now, newest CreatedAt first.
// - RevokeAsync is true only for the call that revoked it: false for an unknown id, another user's session, or one
//   already revoked. EndAsync (the session-end path, no owner check) is true only the first time.
public interface IDeviceSessions
{
    Task<string> CreateCodeAsync(Guid userId, string securityStamp, string challenge, string deviceName, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<DeviceSignIn?> ExchangeAsync(string code, string verifier, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<DeviceSessionRecord?> FindBySecretAsync(string secret, CancellationToken cancellationToken = default);

    Task TouchAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceSessionRecord>> ListAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(Guid userId, Guid id, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<bool> EndAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default);
}
