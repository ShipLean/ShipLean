using Npgsql;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Harness;

[TestFixture]
public class CloneTests
{
    [Test]
    public async Task Clone_WhenTwoClassesRun_ShouldNotShareRows()
    {
        await using var first = await ShipLeanDatabase.CloneEmptyAsync();
        await using var second = await ShipLeanDatabase.CloneEmptyAsync();
        first.Name.ShouldNotBe(second.Name);
        await ExecuteAsync(first, "CREATE TABLE scratch_rows (id int PRIMARY KEY)");
        await ExecuteAsync(second, "CREATE TABLE scratch_rows (id int PRIMARY KEY)");

        await ExecuteAsync(first, "INSERT INTO scratch_rows VALUES (1)");

        (await CountAsync(first)).ShouldBe(1);
        (await CountAsync(second)).ShouldBe(0);
    }

    [Test]
    public async Task Clone_WhenCreated_ShouldUseTheProductionLocale()
    {
        await using var seeded = await ShipLeanDatabase.CloneSeedAsync();
        await using var empty = await ShipLeanDatabase.CloneEmptyAsync();

        foreach (var database in new[] { seeded, empty })
        {
            await using var connection = await database.OpenAsync();
            await using var show = new NpgsqlCommand("SELECT datctype FROM pg_database WHERE datname = current_database()", connection);
            ((string)(await show.ExecuteScalarAsync())!).ShouldBe(FreshDatabase.Locale);
        }
    }

    [Test]
    public async Task Clone_WhenDisposed_ShouldDropTheDatabase()
    {
        var database = await ShipLeanDatabase.CloneEmptyAsync();

        await database.DisposeAsync();

        var ex = await Should.ThrowAsync<PostgresException>(async () => await database.OpenAsync());
        ex.SqlState.ShouldBe(PostgresErrorCodes.InvalidCatalogName);
    }

    [Test]
    public async Task Clone_WhenSeededAndEmpty_ShouldBothHaveTheSchema()
    {
        await using var seeded = await ShipLeanDatabase.CloneSeedAsync();
        await using var empty = await ShipLeanDatabase.CloneEmptyAsync();

        foreach (var database in new[] { seeded, empty })
        {
            await using var connection = await database.OpenAsync();
            await using var journal = new NpgsqlCommand("SELECT count(*) FROM schemaversions WHERE scriptname LIKE 'db/schema/%'", connection);
            ((long)(await journal.ExecuteScalarAsync())!).ShouldBeGreaterThan(0);
        }
    }

    private static async Task ExecuteAsync(ClonedDatabase database, string sql)
    {
        await using var connection = await database.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(ClonedDatabase database)
    {
        await using var connection = await database.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM scratch_rows", connection);
        return (long)(await count.ExecuteScalarAsync())!;
    }
}
