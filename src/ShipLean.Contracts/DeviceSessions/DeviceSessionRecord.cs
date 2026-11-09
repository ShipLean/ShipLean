namespace ShipLean.Contracts.DeviceSessions;

// One device session as stored. SecurityStamp is the user's stamp when the code was made; RevokedAt null while live.
public sealed record DeviceSessionRecord(
    Guid Id,
    Guid UserId,
    string Name,
    string TokenPrefix,
    string SecurityStamp,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt)
{
    // Live: not revoked and not yet expired at now.
    public bool IsLiveAt(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
