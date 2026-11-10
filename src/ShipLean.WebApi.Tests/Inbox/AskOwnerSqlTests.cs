using Dapper;
using Npgsql;
using ShipLean.Contracts.WorkItems;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Inbox;

// AskOwnerSql.OwnedByUser: the owner is coalesce(assigned_to, created_by), still a member of the item's project, and the item is not
// Removed. The database is shared by the class, so every test arranges its own work item.
[TestFixture]
public class AskOwnerSqlTests
{
    private const string MatchSql = $"SELECT EXISTS (SELECT 1 FROM work_item wi WHERE wi.id = @itemId AND {AskOwnerSql.OwnedByUser})";

    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task AskOwnerSql_WhenOwnerIsALiveMember_ShouldMatch()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithAssignee(SeedIds.Users.Tom).WithCreatedBy(SeedIds.Users.Priya).InsertAsync(connection);

        (await MatchesAsync(connection, item, SeedIds.Users.Tom)).ShouldBeTrue();
        (await MatchesAsync(connection, item, SeedIds.Users.Priya)).ShouldBeFalse();
        (await MatchesAsync(connection, item, SeedIds.Users.Mei)).ShouldBeFalse();
    }

    [Test]
    public async Task AskOwnerSql_WhenUnassigned_ShouldUseTheCreator()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithCreatedBy(SeedIds.Users.Tom).InsertAsync(connection);

        (await MatchesAsync(connection, item, SeedIds.Users.Tom)).ShouldBeTrue();
    }

    [Test]
    public async Task AskOwnerSql_WhenOwnerLeftTheProject_ShouldNotMatch()
    {
        await using var connection = await _database.OpenAsync();
        var leaver = await new AppUserBuilder().InsertAsync(connection);
        await connection.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id) VALUES (@id, @project, @leaver)",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, leaver });
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithAssignee(leaver).InsertAsync(connection);
        (await MatchesAsync(connection, item, leaver)).ShouldBeTrue();

        await connection.ExecuteAsync(
            "UPDATE project_member SET archived_at = now() WHERE project_id = @project AND user_id = @leaver",
            new { project = SeedIds.Projects.Ship, leaver });

        (await MatchesAsync(connection, item, leaver)).ShouldBeFalse();
    }

    [Test]
    public async Task AskOwnerSql_WhenItemRemoved_ShouldNotMatch()
    {
        await using var connection = await _database.OpenAsync();

        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithAssignee(SeedIds.Users.Tom).WithState(SeedIds.States.Ship.StoryRemoved)
            .InsertAsync(connection);

        (await MatchesAsync(connection, item, SeedIds.Users.Tom)).ShouldBeFalse();
    }

    private static Task<bool> MatchesAsync(NpgsqlConnection connection, Guid itemId, Guid userId) =>
        connection.ExecuteScalarAsync<bool>(MatchSql, new { itemId, userId });
}
