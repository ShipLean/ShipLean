using System.Text.Json;
using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

// Arranges sprints and backlog items in EMPT (seeded empty, Joel a member). Each test calls ParkBacklogAsync first, so
// the unplanned stack holds only what that test arranges.
public abstract class SprintFixture : McpFixture
{
    private static readonly Guid Parking = new("00000004-0000-4000-8001-0000000000ff");

    protected override bool WithProbeTools => false;

    protected static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    protected async Task ParkBacklogAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO sprint (id, project_id, name, start_date, end_date, rollover_status)
            VALUES (@Parking, @project, 'Parking', @start, @end, 'decided') ON CONFLICT DO NOTHING
            """,
            new { Parking, project = SeedIds.Projects.Empt, start = Today.AddDays(-400), end = Today.AddDays(-387) });
        await connection.ExecuteAsync(
            "UPDATE work_item SET sprint_id = @Parking WHERE project_id = @project AND sprint_id IS NULL",
            new { Parking, project = SeedIds.Projects.Empt });
    }

    protected async Task<Guid> AddSprintAsync(string name, int startOffset, int endOffset, decimal? capacity = null)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, @name, @start, @end)",
            new { id, project = SeedIds.Projects.Empt, name, start = Today.AddDays(startOffset), end = Today.AddDays(endOffset) });
        if (capacity is { } points)
        {
            await connection.ExecuteAsync(
                "INSERT INTO sprint_capacity (id, sprint_id, user_id, capacity_points) VALUES (@capacityId, @id, @user, @points)",
                new { capacityId = Guid.NewGuid(), id, user = SeedIds.Users.Joel, points });
        }
        return id;
    }

    protected async Task<Guid> AddStoryAsync(string title, decimal points, Guid? sprint = null)
    {
        await using var connection = await OpenDatabaseAsync();
        var builder = new WorkItemBuilder(SeedIds.Projects.Empt).WithType("story").WithTitle(title).WithPoints(points);
        return await (sprint is { } id ? builder.WithSprint(id) : builder).InsertAsync(connection);
    }

    protected async Task<string> KeyOfAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @item", new { item });
    }

    protected async Task<Guid?> SprintOfAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @item", new { item });
    }

    protected static string[] Keys(JsonElement result)
        => [.. result.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!)];

    protected static string? ErrorCode(McpReply reply)
    {
        reply.IsToolError().ShouldBeTrue(reply.Body);
        return reply.Structured().GetProperty("error").GetProperty("code").GetString();
    }
}
