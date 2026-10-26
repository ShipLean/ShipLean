using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Schema;

[TestFixture]
public class BuildersTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Builders_WhenRowArranged_ShouldBeReadable()
    {
        await using var connection = await _database.OpenAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Arranged feature").InsertAsync(connection);

        var story = await new WorkItemBuilder(SeedIds.Projects.Ship)
            .WithTitle("Arranged story")
            .WithParent(feature)
            .WithSprint(SeedIds.Sprints.Sprint24)
            .WithStateNamed("Active")
            .WithPoints(3)
            .WithTags("Payments", "Blocked")
            .WithAssignee(SeedIds.Users.Mei)
            .WithBoardPosition("col-in-progress")
            .InsertAsync(connection);

        var row = await connection.QuerySingleAsync<Row>(
            """
            SELECT w.key, w.title, w.parent_id AS parentId, w.parent_type AS parentType, w.sprint_id AS sprintId, state.name AS stateName,
                   w.story_points::float8 AS points, array_to_string(w.tags, ',') AS tags, w.assigned_to AS assignee, w.board_column_id AS boardColumnId, w.revision
            FROM work_item w JOIN state ON state.id = w.state_id WHERE w.id = @story
            """,
            new { story });
        row.Title.ShouldBe("Arranged story");
        row.ParentId.ShouldBe(feature);
        row.ParentType.ShouldBe("feature");
        row.SprintId.ShouldBe(SeedIds.Sprints.Sprint24);
        row.StateName.ShouldBe("Active");
        row.Points.ShouldBe(3);
        row.Tags.ShouldBe("Payments,Blocked");
        row.Assignee.ShouldBe(SeedIds.Users.Mei);
        row.BoardColumnId.ShouldBe("col-in-progress");
        row.Key.ShouldStartWith("SHIP-");
        (await connection.ExecuteScalarAsync<int>("SELECT next_item_number FROM project WHERE id = @id", new { id = SeedIds.Projects.Ship })).ShouldBeGreaterThan(184);
    }

    [Test]
    public async Task Builders_WhenRevisionArranged_ShouldKeepTheRawChanges()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        const string changes = """[{"field":"priority","from":2,"to":1,"fromLabel":null,"toLabel":null}]""";

        var rev = await new RevisionBuilder(item).WithRev(2).WithChangedBy(SeedIds.Users.Priya).WithChanges(changes).InsertAsync(connection);

        rev.ShouldBe(2);
        var stored = await connection.ExecuteScalarAsync<string>("SELECT changes::text FROM revision WHERE work_item_id = @item AND rev = 2", new { item });
        System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(stored!), System.Text.Json.Nodes.JsonNode.Parse(changes)).ShouldBeTrue();
    }

    private sealed record Row(string Key, string Title, Guid ParentId, string ParentType, Guid SprintId, string StateName, double Points, string Tags, Guid Assignee, string BoardColumnId, int Revision);
}
