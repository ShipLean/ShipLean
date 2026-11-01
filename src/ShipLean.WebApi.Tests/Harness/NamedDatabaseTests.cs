using Dapper;
using Npgsql;
using ShipLean.LocalEnvironment.Database;

namespace ShipLean.WebApi.Tests.Harness;

[TestFixture]
public class NamedDatabaseTests
{
    private string _name = null!;

    [SetUp]
    public void ChooseName() => _name = $"shiplean_nt_{Guid.NewGuid():N}"[..21];

    [TearDown]
    public async Task DropDatabase() => await ShipLeanDatabase.DropNamedAsync(_name);

    [Test]
    public async Task OpenNamed_WhenFresh_ShouldReplaceRowsAddedEarlier()
    {
        var first = await ShipLeanDatabase.OpenNamedAsync(_name, NamedDatabaseMode.Fresh);
        await AddProjectAsync(first);

        var second = await ShipLeanDatabase.OpenNamedAsync(_name, NamedDatabaseMode.Fresh);

        (await CountProjectsAsync(second, "NT")).ShouldBe(0);
    }

    [Test]
    public async Task OpenNamed_WhenKeep_ShouldKeepRowsAddedEarlier()
    {
        var first = await ShipLeanDatabase.OpenNamedAsync(_name, NamedDatabaseMode.Fresh);
        await AddProjectAsync(first);

        var second = await ShipLeanDatabase.OpenNamedAsync(_name, NamedDatabaseMode.Keep);

        (await CountProjectsAsync(second, "NT")).ShouldBe(1);
    }

    [Test]
    public async Task OpenNamed_WhenKeepAndMissing_ShouldSeed()
    {
        var database = await ShipLeanDatabase.OpenNamedAsync(_name, NamedDatabaseMode.Keep);

        (await CountProjectsAsync(database)).ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task OpenNamed_WhenEmpty_ShouldHaveTheSchemaAndNoProjects()
    {
        var database = await ShipLeanDatabase.OpenNamedAsync(_name, NamedDatabaseMode.Empty);

        (await CountProjectsAsync(database)).ShouldBe(0);
    }

    [Test]
    public async Task OpenNamed_WhenNameIsInvalid_ShouldThrow()
    {
        await Should.ThrowAsync<ArgumentException>(() => ShipLeanDatabase.OpenNamedAsync("Bad-Name", NamedDatabaseMode.Fresh));
    }

    [TestCase("shiplean_local", true)]
    [TestCase("shiplean_nt_ab12cd34", true)]
    [TestCase("Bad-Name", false)]
    [TestCase("postgres", false)]
    [TestCase("template1", false)]
    [TestCase("shiplean", false)]
    [TestCase("shiplean_seed_x", false)]
    [TestCase("shiplean_f_2611011200_x", false)]
    [TestCase("shiplean_s311-l1", false)]
    public void IsValidName_WhenNameIsGiven_ShouldMatchTheRule(string name, bool expected)
    {
        ShipLeanDatabase.IsValidName(name).ShouldBe(expected);
    }

    [Test]
    public async Task DropNamed_WhenDatabaseExists_ShouldDropIt()
    {
        var database = await ShipLeanDatabase.OpenNamedAsync(_name, NamedDatabaseMode.Empty);

        await ShipLeanDatabase.DropNamedAsync(_name);

        var ex = await Should.ThrowAsync<PostgresException>(async () => await CountProjectsAsync(database));
        ex.SqlState.ShouldBe(PostgresErrorCodes.InvalidCatalogName);
    }

    private static async Task AddProjectAsync(NamedDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO project (id, key, name) VALUES (gen_random_uuid(), 'NT', 'Named database test')");
    }

    private static async Task<long> CountProjectsAsync(NamedDatabase database, string? key = null)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM project WHERE @key::text IS NULL OR key = @key", new { key });
    }
}
