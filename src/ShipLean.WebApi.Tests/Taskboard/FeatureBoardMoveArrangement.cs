using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Taskboard;

// A SHIP Feature with a story (Backlog) and a bug under it, a nested Feature whose story sits in a sprint, a task under
// each, and a story under an unrelated Feature, built straight into the database. Task ids ascend in insertion order.
public sealed record FeatureBoardMoveArrangement(
    Guid Feature, Guid NestedFeature, Guid Story, Guid Bug, Guid NestedStory, Guid NestedSprint,
    Guid StoryTaskOne, Guid StoryTaskTwo, Guid NestedTask, Guid OutsideStory, Guid OutsideTask)
{
    public static async Task<FeatureBoardMoveArrangement> InsertAsync(NpgsqlConnection connection, string name)
    {
        var prefix = Guid.NewGuid().ToString("N")[..24];
        var next = 0;
        Guid TaskId() => Guid.ParseExact(prefix + (++next).ToString("x8", System.Globalization.CultureInfo.InvariantCulture), "N");
        var sprint = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Ship, name + " (sprint)", 1200);
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle(name).InsertAsync(connection);
        var nested = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle(name + " nested").WithParent(feature).InsertAsync(connection);
        var other = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle(name + " other").InsertAsync(connection);
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Board story").WithParent(feature).InsertAsync(connection);
        var bug = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bug").WithTitle("Board bug").WithParent(feature).InsertAsync(connection);
        var nestedStory = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Nested story").WithParent(nested).WithSprint(sprint).InsertAsync(connection);
        var outside = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Outside story").WithParent(other).InsertAsync(connection);
        var one = await InsertTask(connection, TaskId(), story, "First task");
        var two = await InsertTask(connection, TaskId(), story, "Second task");
        var nestedTask = await InsertTask(connection, TaskId(), nestedStory, "Nested task");
        var outsideTask = await InsertTask(connection, TaskId(), outside, "Outside task");
        return new FeatureBoardMoveArrangement(feature, nested, story, bug, nestedStory, sprint, one, two, nestedTask, outside, outsideTask);
    }

    private static Task<Guid> InsertTask(NpgsqlConnection connection, Guid id, Guid parent, string title)
        => new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithId(id).WithTitle(title).WithParent(parent).InsertAsync(connection);
}
