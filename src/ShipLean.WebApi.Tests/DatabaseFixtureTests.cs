using Npgsql;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests;

[TestFixture]
public class DatabaseFixtureTests
{
    [Test]
    public async Task FreshDatabase_WhenCreated_ShouldApplySchema()
    {
        await using var database = await FreshDatabase.CreateAsync();
        await using var connection = await database.OpenAsync();

        await using var table = new NpgsqlCommand("SELECT to_regclass('public.work_item') IS NOT NULL", connection);
        ((bool)(await table.ExecuteScalarAsync())!).ShouldBeTrue();
        await using var journal = new NpgsqlCommand("SELECT count(*) FROM schemaversions WHERE scriptname LIKE 'db/schema/%'", connection);
        ((long)(await journal.ExecuteScalarAsync())!).ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task FreshDatabase_WhenTwoAreCreated_ShouldBeIsolated()
    {
        await using var first = await FreshDatabase.CreateAsync();
        await using var second = await FreshDatabase.CreateAsync();
        first.Name.ShouldNotBe(second.Name);

        await using (var connection = await first.OpenAsync())
        await using (var insert = new NpgsqlCommand("INSERT INTO data_protection_key (friendly_name, xml) VALUES ('iso', '<key/>')", connection))
        {
            await insert.ExecuteNonQueryAsync();
        }

        (await CountMetaAsync(first)).ShouldBe(1);
        (await CountMetaAsync(second)).ShouldBe(0);
    }

    [Test]
    public async Task FreshDatabase_WhenCreated_ShouldUseTheProductionLocale()
    {
        await using var database = await FreshDatabase.CreateAsync();
        await using var connection = await database.OpenAsync();

        await using var show = new NpgsqlCommand("SELECT datctype FROM pg_database WHERE datname = current_database()", connection);
        ((string)(await show.ExecuteScalarAsync())!).ShouldBe(FreshDatabase.Locale);
    }

    [Test]
    public async Task FreshDatabase_WhenDisposed_ShouldDropTheDatabase()
    {
        var database = await FreshDatabase.CreateAsync();
        var connection = await database.OpenAsync();
        await connection.DisposeAsync();

        await database.DisposeAsync();

        var ex = await Should.ThrowAsync<PostgresException>(async () => await database.OpenAsync());
        ex.SqlState.ShouldBe(PostgresErrorCodes.InvalidCatalogName);
    }

    private static async Task<long> CountMetaAsync(FreshDatabase database)
    {
        await using var connection = await database.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM data_protection_key", connection);
        return (long)(await count.ExecuteScalarAsync())!;
    }
}
