using ShipLean.Application.Identity;
using ShipLean.Contracts.DeviceSessions;

namespace ShipLean.Application.Fakes.DeviceSessions;

// IDeviceSessions in memory, with every pinned rule (DeviceSessionsContract). One lock makes each call atomic, so of two
// exchanges of one code exactly one wins. Codes and secrets are kept only as hashes, as the Postgres store keeps them.
public sealed class InMemoryDeviceSessions : IDeviceSessions
{
    private readonly object _gate = new();
    private readonly List<Code> _codes = [];
    private readonly List<Session> _sessions = [];

    public Task<string> CreateCodeAsync(Guid userId, string securityStamp, string challenge, string deviceName, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var code = DeviceSecret.NewCode();
        lock (_gate)
        {
            _codes.Add(new Code(DeviceSecret.Hash(code), userId, securityStamp, challenge, deviceName, now + DeviceSessionLimits.CodeLifetime));
        }
        return Task.FromResult(code);
    }

    public Task<DeviceSignIn?> ExchangeAsync(string code, string verifier, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var hash = DeviceSecret.Hash(code);
        lock (_gate)
        {
            var found = _codes.FirstOrDefault(c => DeviceSecret.Same(hash, c.Hash));
            if (found is null || found.Used || found.ExpiresAt <= now)
            {
                return Task.FromResult<DeviceSignIn?>(null);
            }
            // Used up before the verifier is checked: a wrong verifier spends the code.
            found.Used = true;
            if (!DeviceSecret.VerifierMatches(verifier, found.Challenge))
            {
                return Task.FromResult<DeviceSignIn?>(null);
            }
            var secret = DeviceSecret.New();
            var session = new Session(
                Guid.CreateVersion7(now), found.UserId, found.DeviceName, DeviceSecret.Hash(secret), DeviceSecret.TokenPrefix(secret),
                found.SecurityStamp, now)
            {
                LastUsedAt = now,
                ExpiresAt = now + DeviceSessionLimits.SessionLifetime,
            };
            _sessions.Add(session);
            return Task.FromResult<DeviceSignIn?>(new DeviceSignIn(session.ToRecord(), secret));
        }
    }

    public Task<DeviceSessionRecord?> FindBySecretAsync(string secret, CancellationToken cancellationToken = default)
    {
        var hash = DeviceSecret.Hash(secret);
        lock (_gate)
        {
            return Task.FromResult(_sessions.FirstOrDefault(s => DeviceSecret.Same(hash, s.Hash))?.ToRecord());
        }
    }

    public Task<DeviceSessionRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id)?.ToRecord());
        }
    }

    public Task TouchAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_sessions.FirstOrDefault(s => s.Id == id) is { RevokedAt: null } session && session.ExpiresAt > now && now - session.LastUsedAt >= DeviceSessionLimits.TouchInterval)
            {
                session.LastUsedAt = now;
                session.ExpiresAt = now + DeviceSessionLimits.SessionLifetime;
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeviceSessionRecord>> ListAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<DeviceSessionRecord> list = _sessions
                .Where(s => s.UserId == userId)
                .Select(s => s.ToRecord())
                .Where(r => r.IsLiveAt(now))
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<bool> RevokeAsync(Guid userId, Guid id, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(RevokeLocked(_sessions.FirstOrDefault(s => s.Id == id && s.UserId == userId), now));
        }
    }

    public Task<bool> EndAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(RevokeLocked(_sessions.FirstOrDefault(s => s.Id == id), now));
        }
    }

    private static bool RevokeLocked(Session? session, DateTimeOffset now)
    {
        if (session is not { RevokedAt: null })
        {
            return false;
        }
        session.RevokedAt = now;
        return true;
    }

    private sealed record Code(byte[] Hash, Guid UserId, string SecurityStamp, string Challenge, string DeviceName, DateTimeOffset ExpiresAt)
    {
        public bool Used { get; set; }
    }

    private sealed record Session(Guid Id, Guid UserId, string Name, byte[] Hash, string TokenPrefix, string SecurityStamp, DateTimeOffset CreatedAt)
    {
        public DateTimeOffset LastUsedAt { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }

        public DateTimeOffset? RevokedAt { get; set; }

        public DeviceSessionRecord ToRecord() => new(Id, UserId, Name, TokenPrefix, SecurityStamp, CreatedAt, LastUsedAt, ExpiresAt, RevokedAt);
    }
}
