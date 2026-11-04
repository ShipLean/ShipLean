using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.Application.Features.Demos.Runs;

namespace ShipLean.WebApi.Tests.Demos;

// A project with a live connection and repository, and sprints of fresh dates, for the run's tests.
internal static class DemoRunArrange
{
    private static int _next;

    public static async Task<Guid> NewSprintAsync(NpgsqlConnection database, Guid projectId, string name = "Sprint 23")
    {
        var id = Guid.NewGuid();
        var start = new DateOnly(2100, 1, 1).AddDays(14 * Interlocked.Increment(ref _next));
        await database.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @projectId, @name, @start, @end)", new { id, projectId, name, start, end = start.AddDays(13) });
        return id;
    }

    public static DemoRunRequest Request(Guid projectId, Guid sprintId, Guid itemId, string via = "button")
        => new(projectId, sprintId, itemId, SeedIds.Users.Joel, via, "https://planner.example.com", "ShipLean", null);

    public static async Task<(Guid ProjectId, Guid SprintId, Guid ItemId)> NewDemoAsync(NpgsqlConnection database, ShipLean.TestSupport.Fixtures.ApiClient api, IDemoRunStarter starter)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, api);
        var sprintId = await NewSprintAsync(database, arranged.Project.Id);
        return (arranged.Project.Id, sprintId, await starter.GetOrCreateItemAsync(arranged.Project.Id, sprintId, "Sprint 23", CancellationToken.None));
    }

    // The token inside the delivery's prompt: the part of the upload link after token=.
    public static string TokenIn(string deliveryBody)
    {
        var start = deliveryBody.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        var end = deliveryBody.IndexOfAny(['"', '\\', ' ', '<', '&'], start);
        end = end < 0 ? deliveryBody.Length : end;
        return Uri.UnescapeDataString(deliveryBody[start..end]);
    }
}
