using Dapper;
using Npgsql;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.TaskChanges.Domain;

// One live row per commit on an item; an archived row of the same commit does not count.
[TestFixture]
public class WorkItemCommitIndexTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task WorkItemCommitIndex_WhenArchivedDuplicate_ShouldAllowLiveRow()
    {
        await using var connection = await _database.OpenAsync();
        var (item, repository) = await ArrangeAsync(connection);
        await InsertAsync(connection, item, repository, 1, archived: true);

        await InsertAsync(connection, item, repository, 2, archived: false);

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item_commit WHERE work_item_id = @item", new { item })).ShouldBe(2);
    }

    [Test]
    public async Task WorkItemCommitIndex_WhenLiveDuplicate_ShouldRefuse()
    {
        await using var connection = await _database.OpenAsync();
        var (item, repository) = await ArrangeAsync(connection);
        await InsertAsync(connection, item, repository, 1, archived: false);

        var duplicate = async () => await InsertAsync(connection, item, repository, 2, archived: false);

        (await duplicate.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    private static async Task<(Guid Item, Guid Repository)> ArrangeAsync(NpgsqlConnection connection)
    {
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        var repository = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO code_repository (id, provider, host, path) VALUES (@repository, 'github', 'github.com', @path)",
            new { repository, path = $"acme/{repository:N}" });
        return (item, repository);
    }

    private static Task InsertAsync(NpgsqlConnection connection, Guid item, Guid repository, int position, bool archived)
        => connection.ExecuteAsync(
            """
            INSERT INTO work_item_commit (id, work_item_id, repository_id, sha, position, added_via, archived_at)
            VALUES (gen_random_uuid(), @item, @repository, @sha, @position, 'add_commits', CASE WHEN @archived THEN now() END)
            """,
            new { item, repository, sha = Sha, position, archived });
}
