using Npgsql;
using ShipLean.Application.Database;

namespace ShipLean.TestSupport.Database;

// A new, uniquely named database on the test server with db/schema, db/migrations and db/seed applied. Disposing
// drops it, closing any connection still open. Each call builds from the scripts; the test harness adds template
// cloning on top of this.
public sealed class FreshDatabase : IAsyncDisposable
{
    // The locale of the postgres:18 image the app runs on in production and CI (text comparison and ORDER BY depend on
    // it), so a server with another default still tests what ships.
    public const string Locale = "en_US.UTF-8";

    private readonly string _adminConnectionString;

    private FreshDatabase(string name, string connectionString, string adminConnectionString)
    {
        Name = name;
        ConnectionString = connectionString;
        _adminConnectionString = adminConnectionString;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public static async Task<FreshDatabase> CreateAsync(bool includeSeed = true)
    {
        DapperTypeHandlers.Register();
        var admin = await PostgresServer.AdminConnectionStringAsync();
        var name = $"shiplean_f_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand(
                $"CREATE DATABASE \"{name}\" TEMPLATE template0 ENCODING 'UTF8' LOCALE_PROVIDER libc LOCALE '{Locale}'", connection);
            await create.ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = true }.ConnectionString;
        DatabaseMigrator.Apply(connectionString, includeSeed);
        return new FreshDatabase(name, connectionString, admin);
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        // Only this database's pool: other test classes in the process keep theirs.
        await using (var own = new NpgsqlConnection(ConnectionString))
        {
            NpgsqlConnection.ClearPool(own);
        }
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
