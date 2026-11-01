using Agoda.IoC.Core;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Usage;

// Name and email of a user, read by SQL for the analytics person. Used only when the instance's SendNames setting is on; cached for
// ten minutes, at most 1000 users.
[RegisterSingleton]
public sealed class PersonLookup(NpgsqlDataSource dataSource, TimeProvider time)
{
    public sealed record Person(string Name, string Email);

    private readonly LookupCache<Guid, Person> _cache = new(time, TimeSpan.FromMinutes(10), 1000);

    public async Task<Person?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_cache.TryGet(userId, out var cached))
        {
            return cached;
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var person = await connection.QuerySingleOrDefaultAsync<Person>(new CommandDefinition(
            "SELECT name AS Name, email AS Email FROM app_user WHERE id = @id", new { id = userId }, cancellationToken: cancellationToken));
        if (person is not null)
        {
            _cache.Set(userId, person);
        }
        return person;
    }
}
