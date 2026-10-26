using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.TestSupport.Database;
using ShipLean.Contracts.Users;

namespace ShipLean.WebApi.Tests.Users;

// A fresh empty database per test (the seed has two active admins, which would make the admin rules order-dependent),
// a data source on it, and the arrange hooks of the contract fixtures.
internal sealed class PostgresUsersStore : IAsyncDisposable
{
    private static int _projectCounter;

    private readonly ClonedDatabase _database;

    private PostgresUsersStore(ClonedDatabase database)
    {
        _database = database;
        DataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(database.ConnectionString));
    }

    public NpgsqlDataSource DataSource { get; }

    public static async Task<PostgresUsersStore> CreateAsync()
    {
        DapperTypeHandlers.Register();
        return new PostgresUsersStore(await ShipLeanDatabase.CloneEmptyAsync());
    }

    public async Task<ArrangedUser> ArrangeUserAsync(UserSpec spec)
    {
        var (name, email) = spec.NewIdentity();
        var now = TimeProvider.System.GetUtcNow();
        var builder = new AppUserBuilder().WithName(name).WithEmail(email);
        builder = spec.Status switch
        {
            UserStatus.NoPassword => builder.WithPassword(null),
            UserStatus.OneTimePassword => builder.MustChangePassword(),
            UserStatus.Locked => builder.LockedUntil(now.AddHours(1)),
            UserStatus.Disabled => builder.DisabledAt(now),
            _ => builder,
        };
        if (spec.IsInstanceAdmin)
        {
            builder = builder.Admin();
        }
        await using var connection = await _database.OpenAsync();
        var id = await builder.InsertAsync(connection);
        foreach (var projectId in spec.ProjectIds)
        {
            await connection.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id) VALUES (gen_random_uuid(), @projectId, @id)", new { projectId, id });
        }
        return new ArrangedUser(id, email, name);
    }

    public async Task<Guid> ArrangeProjectAsync()
    {
        var number = Interlocked.Increment(ref _projectCounter);
        var id = Guid.NewGuid();
        var key = $"{(char)('A' + (number / 26 % 26))}{(char)('A' + (number % 26))}";
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO project (id, key, name) VALUES (@id, @key, @name)", new { id, key, name = "Arranged project " + key });
        return id;
    }

    public async Task ArchiveMembershipAsync(Guid projectId, Guid userId)
    {
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync(
            "UPDATE project_member SET archived_at = now() WHERE project_id = @projectId AND user_id = @userId", new { projectId, userId });
    }

    public async Task<int> CountUsersWithEmailAsync(string email)
    {
        await using var connection = await _database.OpenAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) = lower(@email)", new { email });
    }

    public async Task<DateTimeOffset?> LastSignInAtAsync(Guid id)
    {
        await using var connection = await _database.OpenAsync();
        return await connection.ExecuteScalarAsync<DateTimeOffset?>("SELECT last_sign_in_at FROM app_user WHERE id = @id", new { id });
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _database.DisposeAsync();
    }
}
