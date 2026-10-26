using System.Collections.Concurrent;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Identity;

// app_user as each request sees it, cached for a minute so a signed-in page does not cost a query per request. The
// code that changes a user in this process (sign-in, password change, sign out others) calls Forget; a change made by
// another process, or by the accounts service, is seen after at most Lifetime.
[RegisterSingleton]
public class UserSessions(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private sealed record Entry(UserSnapshot? User, DateTimeOffset ReadAt);

    private readonly ConcurrentDictionary<Guid, Entry> _cache = new();

    // Bumped by Forget. A read keeps its result only if the generation is still the one it started under, so a slow
    // read that began before a change cannot put the old stamp back after Forget.
    private readonly ConcurrentDictionary<Guid, long> _generations = new();

    // Null when no such user exists.
    public async Task<UserSnapshot?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        return _cache.TryGetValue(userId, out var cached) && now - cached.ReadAt < Lifetime
            ? cached.User
            : await ReadAsync(userId, now, cancellationToken);
    }

    // Always reads the row (and refreshes the cache): for rights that must change the moment they are removed, such
    // as instance admin, which demoting does not end the session over.
    public Task<UserSnapshot?> GetFreshAsync(Guid userId, CancellationToken cancellationToken = default)
        => ReadAsync(userId, clock.GetUtcNow(), cancellationToken);

    private async Task<UserSnapshot?> ReadAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var generation = _generations.GetValueOrDefault(userId);
        var user = await QueryAsync(userId, cancellationToken);
        if (_generations.GetValueOrDefault(userId) == generation)
        {
            var entry = new Entry(user, now);
            _cache[userId] = entry;
            // A Forget between the check and the store bumped the generation after it: take this entry back out.
            if (_generations.GetValueOrDefault(userId) != generation)
            {
                _cache.TryRemove(KeyValuePair.Create(userId, entry));
            }
        }
        return user;
    }

    protected virtual async Task<UserSnapshot?> QueryAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserSnapshot>(new CommandDefinition(
            """
            SELECT id, is_admin AS IsAdmin, must_change_password AS MustChangePassword,
                   disabled_at IS NOT NULL AS Disabled, security_stamp AS SecurityStamp
            FROM app_user WHERE id = @userId
            """, new { userId }, cancellationToken: cancellationToken));
    }

    public void Forget(Guid userId)
    {
        _generations.AddOrUpdate(userId, 1, (_, generation) => generation + 1);
        _cache.TryRemove(userId, out _);
    }
}
