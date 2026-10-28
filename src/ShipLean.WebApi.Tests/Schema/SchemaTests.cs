using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Schema;

[TestFixture]
public class SchemaTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Schema_WhenApplied_ShouldHaveEveryIssueOneTable()
    {
        string[] expected =
        [
            "agent_activity", "agent_connection", "agent_delivery", "agent_session", "app_user", "attachment", "board", "comment", "data_protection_key", "import_error", "import_job", "import_log",
            "import_mapping", "instance_settings", "jira_connection", "link_state", "mcp_capture_plan_apply", "oauth_authorization", "oauth_client", "oauth_code",
            "oauth_request", "oauth_token", "project", "project_member", "revision", "search_doc", "sprint",
            "sprint_capacity", "state", "transfer_token", "user_preference", "webhook_delivery", "work_item", "work_item_link",
        ];
        await using var connection = await _database.OpenAsync();

        var tables = await connection.QueryAsync<string>(
            "SELECT table_name::text FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> 'schemaversions' ORDER BY 1");

        tables.ToArray().ShouldBe(expected);
    }

    [TestCase("epic", "epic", false)]
    [TestCase("epic", "feature", false)]
    [TestCase("feature", "epic", true)]
    [TestCase("feature", "feature", true)]
    [TestCase("feature", "story", false)]
    [TestCase("story", "feature", true)]
    [TestCase("story", "epic", false)]
    [TestCase("story", "story", false)]
    [TestCase("story", "task", false)]
    [TestCase("bug", "feature", true)]
    [TestCase("bug", "story", false)]
    [TestCase("task", "story", true)]
    [TestCase("task", "bug", true)]
    [TestCase("task", "feature", false)]
    [TestCase("task", "task", false)]
    public async Task WorkItem_WhenParentTypeNotAllowed_ShouldBeRejected(string type, string parentType, bool allowed)
    {
        await using var connection = await _database.OpenAsync();
        var parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(parentType).InsertAsync(connection);
        var child = new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithParent(parent);

        if (allowed)
        {
            await child.InsertAsync(connection);
        }
        else
        {
            var ex = await Should.ThrowAsync<PostgresException>(async () => await child.InsertAsync(connection));
            ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        }
    }

    [Test]
    public async Task WorkItem_WhenParentTypeDoesNotMatchTheParent_ShouldBeRejectedByTheForeignKey()
    {
        await using var connection = await _database.OpenAsync();
        var parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").InsertAsync(connection);
        var child = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithParent(parent).InsertAsync(connection);

        var ex = await Should.ThrowAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("UPDATE work_item SET parent_type = 'epic' WHERE id = @child", new { child }));

        ex.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Test]
    public async Task WorkItem_WhenParentTypeSetWithoutParent_ShouldBeRejectedByTheCheck()
    {
        await using var connection = await _database.OpenAsync();
        var parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").InsertAsync(connection);
        var child = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithParent(parent).InsertAsync(connection);

        var ex = await Should.ThrowAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("UPDATE work_item SET parent_id = NULL WHERE id = @child", new { child }));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task WorkItem_WhenParentIsOptional_ShouldBeAccepted()
    {
        await using var connection = await _database.OpenAsync();

        var ids = new List<Guid>();
        foreach (var type in new[] { "epic", "feature", "story", "bug", "task" })
        {
            ids.Add(await new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).InsertAsync(connection));
        }

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE id = ANY(@ids) AND parent_id IS NULL", new { ids })).ShouldBe(5);
    }

    [Test]
    public async Task WorkItem_WhenStateBelongsToAnotherType_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();

        var ex = await Should.ThrowAsync<PostgresException>(async () =>
            await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithState(SeedIds.States.Ship.TaskNew).InsertAsync(connection));

        ex.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Test]
    public async Task ProjectKey_WhenNotTwoToFourUppercaseLetters_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();

        foreach (var key in new[] { "A", "ABCDE", "ab", "AB1" })
        {
            var ex = await Should.ThrowAsync<PostgresException>(async () => await connection.ExecuteAsync(
                "INSERT INTO project (id, key, name) VALUES (gen_random_uuid(), @key, 'x')", new { key }));
            ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        }
    }
}
