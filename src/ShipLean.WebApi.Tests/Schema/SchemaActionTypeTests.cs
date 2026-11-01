using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Schema;

[TestFixture]
public class SchemaActionTypeTests
{
    private const string Script = "202611020900_workitems_action_type.sql";

    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [TestCase("action", "feature", true)]
    [TestCase("action", "story", false)]
    [TestCase("action", "epic", false)]
    [TestCase("action", "action", false)]
    [TestCase("task", "action", true)]
    [TestCase("story", "action", false)]
    public async Task WorkItem_WhenActionIsInTheParentRule_ShouldAcceptOnlyTheAllowedParents(string type, string parentType, bool allowed)
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
    public async Task WorkItem_WhenActionHasNoParent_ShouldBeAccepted()
    {
        await using var connection = await _database.OpenAsync();

        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").InsertAsync(connection);

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE id = @id AND type = 'action' AND parent_id IS NULL", new { id })).ShouldBe(1);
    }

    [Test]
    public async Task State_WhenSeeded_ShouldGiveEveryProjectExactlyFourActionStates()
    {
        await using var connection = await _database.OpenAsync();

        var counts = (await connection.QueryAsync<int>(
            "SELECT (SELECT count(*)::int FROM state s WHERE s.project_id = p.id AND s.type = 'action' AND s.archived_at IS NULL) FROM project p")).ToList();

        counts.ShouldNotBeEmpty();
        counts.ShouldAllBe(c => c == 4);
    }

    [Test]
    public async Task Migration_WhenAProjectHasStatesButNoActionStates_ShouldAddTheFour()
    {
        await using var connection = await _database.OpenAsync();
        var projectId = Guid.NewGuid();
        await connection.ExecuteAsync("INSERT INTO project (id, key, name) VALUES (@projectId, 'MIGR', 'Migrated')", new { projectId });
        await connection.ExecuteAsync(
            "INSERT INTO state (id, project_id, type, name, category, sort_order) VALUES (gen_random_uuid(), @projectId, 'story', 'New', 'proposed', 1)", new { projectId });
        await connection.ExecuteAsync(ReadScript());

        var states = (await connection.QueryAsync<ActionState>(
            "SELECT name, category, sort_order AS sortOrder FROM state WHERE project_id = @projectId AND type = 'action' ORDER BY sort_order", new { projectId })).ToList();

        states.Select(s => s.Name).ShouldBe(["New", "Active", "Closed", "Removed"]);
        states.Select(s => s.Category).ShouldBe(["proposed", "inProgress", "completed", "removed"]);
        states.Select(s => s.SortOrder).ShouldBe([1, 2, 3, 4]);
    }

    private sealed record ActionState(string Name, string Category, int SortOrder);

    private static string ReadScript()
    {
        var assembly = typeof(DatabaseScripts).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(Script, StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }
}
