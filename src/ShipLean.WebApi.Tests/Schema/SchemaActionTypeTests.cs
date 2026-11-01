using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Contracts.Refs;
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
    public async Task StoryLevelDb_WhenBoundToAny_ShouldMatchStoryBugAndAction()
    {
        await using var connection = await _database.OpenAsync();

        var types = (await connection.QueryAsync<string>(
            "SELECT t FROM unnest(ARRAY['epic','feature','story','bug','task','action']) AS t WHERE t = ANY(@storyLevel) ORDER BY t",
            new { storyLevel = WorkItemTypeRules.StoryLevelDb })).ToList();

        types.ShouldBe(["action", "bug", "story"]);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldHaveTheShipActionItemUnderAFeature()
    {
        await using var connection = await _database.OpenAsync();

        var row = await connection.QuerySingleAsync<(string Type, string ParentType, string State, bool HasSprint, bool HasPoints)>(
            """
            SELECT w.type, p.type, s.name, w.sprint_id IS NOT NULL, w.story_points IS NOT NULL
            FROM work_item w
            JOIN work_item p ON p.id = w.parent_id
            JOIN state s ON s.id = w.state_id
            WHERE w.id = @id
            """, new { id = SeedIds.WorkItems.Ship89 });

        row.ShouldBe(("action", "feature", "New", false, false));
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
