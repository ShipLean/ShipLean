using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Schema;

[TestFixture]
public class SchemaBauTypeTests
{
    private const string Script = "202611051000_workitems_bau_type.sql";

    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Bau_WhenInserted_ShouldHaveNoParentOrSprint()
    {
        await using var connection = await _database.OpenAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").InsertAsync(connection);

        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bau").InsertAsync(connection);

        var withParent = await Should.ThrowAsync<PostgresException>(async () => await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bau").WithParent(feature).InsertAsync(connection));
        withParent.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        var withSprint = await Should.ThrowAsync<PostgresException>(async () => await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bau").WithSprint(SeedIds.Sprints.Sprint24).InsertAsync(connection));
        withSprint.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        withSprint.ConstraintName.ShouldBe("ck_work_item_bau_alone");
    }

    [Test]
    public async Task Task_WhenParentIsBau_ShouldBeRefused()
    {
        await using var connection = await _database.OpenAsync();
        var bau = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bau").InsertAsync(connection);

        var ex = await Should.ThrowAsync<PostgresException>(async () => await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithParent(bau).InsertAsync(connection));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task Migration_ShouldGiveEveryProjectFourBauStates()
    {
        await using var connection = await _database.OpenAsync();
        var projectId = Guid.NewGuid();
        await connection.ExecuteAsync("INSERT INTO project (id, key, name) VALUES (@projectId, 'MIGB', 'Migrated')", new { projectId });
        await connection.ExecuteAsync(
            "INSERT INTO state (id, project_id, type, name, category, sort_order) VALUES (gen_random_uuid(), @projectId, 'story', 'New', 'proposed', 1)", new { projectId });
        await connection.ExecuteAsync("ALTER TABLE work_item DROP CONSTRAINT ck_work_item_bau_alone");
        await connection.ExecuteAsync(ReadScript());

        var states = (await connection.QueryAsync<BauState>(
            "SELECT name, category, sort_order AS sortOrder FROM state WHERE project_id = @projectId AND type = 'bau' ORDER BY sort_order", new { projectId })).ToList();
        states.Select(s => s.Name).ShouldBe(["New", "Active", "Closed", "Removed"]);
        states.Select(s => s.Category).ShouldBe(["proposed", "inProgress", "completed", "removed"]);
        states.Select(s => s.SortOrder).ShouldBe([1, 2, 3, 4]);

        var counts = (await connection.QueryAsync<int>(
            "SELECT (SELECT count(*)::int FROM state s WHERE s.project_id = p.id AND s.type = 'bau' AND s.archived_at IS NULL) FROM project p")).ToList();
        counts.ShouldNotBeEmpty();
        counts.ShouldAllBe(c => c == 4);
    }

    private sealed record BauState(string Name, string Category, int SortOrder);

    private static string ReadScript()
    {
        var assembly = typeof(DatabaseScripts).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(Script, StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }
}
