using Agoda.IoC.Core;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Usage;

// Project id to project key for the analytics events, read by SQL and cached in memory for ten minutes (the key never changes, so the
// cache only bounds the queries). At most 1000 projects are held.
[RegisterSingleton]
public sealed class ProjectKeyLookup(NpgsqlDataSource dataSource, TimeProvider time)
{
    private readonly LookupCache<Guid, string> _cache = new(time, TimeSpan.FromMinutes(10), 1000);

    public async Task<string?> FindAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (_cache.TryGet(projectId, out var cached))
        {
            return cached;
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var key = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT key FROM project WHERE id = @id", new { id = projectId }, cancellationToken: cancellationToken));
        if (key is not null)
        {
            _cache.Set(projectId, key);
        }
        return key;
    }
}
