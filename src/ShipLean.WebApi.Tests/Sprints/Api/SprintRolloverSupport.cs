using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// Arranges an ended SHIP sprint whose rollover is pending, with two unfinished stories in it.
public static class SprintRolloverSupport
{
    public static async Task<(Guid SprintId, Guid FirstStory, Guid SecondStory)> ArrangeEndedSprintAsync(NpgsqlConnection connection, string name)
    {
        var sprintId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date, rollover_status) VALUES (@sprintId, @projectId, @name, @start, @end, 'pending')",
            new { sprintId, projectId = SeedIds.Projects.Ship, name, start = today.AddDays(-400), end = today.AddDays(-387) });
        var first = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Leftover one").WithPoints(3).WithSprint(sprintId).InsertAsync(connection);
        var second = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bug").WithTitle("Leftover two").WithPoints(2).WithSprint(sprintId).InsertAsync(connection);
        return (sprintId, first, second);
    }

    public static async Task<Guid> ArrangeEmptyEndedSprintAsync(NpgsqlConnection connection, string name)
    {
        var sprintId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date, rollover_status) VALUES (@sprintId, @projectId, @name, @start, @end, 'pending')",
            new { sprintId, projectId = SeedIds.Projects.Ship, name, start = today.AddDays(-370), end = today.AddDays(-357) });
        return sprintId;
    }
}
