using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Taskboard;

// A SHIP sprint with a story, a bug, a story in another sprint and tasks under them, built straight into the database.
// Tasks carry no order key, as tasks created through the API don't, and their ids ascend in insertion order, so the first task
// inserted under a parent sorts first (the cell order is `column_order NULLS LAST, id`).
public sealed record TaskboardArrangement(
    Guid SprintId, Guid Story, Guid Bug, Guid OtherSprintStory,
    Guid StoryOpenOne, Guid StoryOpenTwo, Guid BugTask, Guid Unparented, Guid OtherSprintTask)
{
    public static async Task<TaskboardArrangement> InsertAsync(NpgsqlConnection connection, string name)
    {
        var ids = new AscendingIds();
        var sprint = await InsertSprintAsync(connection, SeedIds.Projects.Ship, name, 400);
        var other = await InsertSprintAsync(connection, SeedIds.Projects.Ship, name + " (other)", 800);
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Sprint story").WithSprint(sprint).WithPoints(3).InsertAsync(connection);
        var bug = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bug").WithTitle("Sprint bug").WithSprint(sprint).InsertAsync(connection);
        var outside = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Other sprint story").WithSprint(other).InsertAsync(connection);
        var one = await InsertTask(connection, ids, story, "First task");
        var two = await InsertTask(connection, ids, story, "Second task");
        var bugTask = await InsertTask(connection, ids, bug, "Bug task");
        var outsideTask = await InsertTask(connection, ids, outside, "Task of the other sprint's story");
        var unparented = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithId(ids.Next()).WithTitle("Loose task").WithSprint(sprint).InsertAsync(connection);
        return new TaskboardArrangement(sprint, story, bug, outside, one, two, bugTask, unparented, outsideTask);
    }

    public static async Task<Guid> InsertSprintAsync(NpgsqlConnection connection, Guid projectId, string name, int daysAhead)
    {
        var id = Guid.NewGuid();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @projectId, @name, @start, @end)",
            new { id, projectId, name, start = today.AddDays(daysAhead + 3650), end = today.AddDays(daysAhead + 3663) });
        return id;
    }

    private static async Task<Guid> InsertTask(NpgsqlConnection connection, AscendingIds ids, Guid parent, string title)
        => await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithId(ids.Next()).WithTitle(title).WithParent(parent).InsertAsync(connection);

    // Ids that sort in the order they're handed out, under a prefix of their own.
    private sealed class AscendingIds
    {
        private readonly string _prefix = Guid.NewGuid().ToString("N")[..24];
        private int _next;

        public Guid Next() => Guid.ParseExact(_prefix + (++_next).ToString("x8", System.Globalization.CultureInfo.InvariantCulture), "N");
    }
}
