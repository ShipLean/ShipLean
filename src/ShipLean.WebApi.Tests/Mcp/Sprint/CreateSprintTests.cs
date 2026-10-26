using System.Net;
using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

[TestFixture]
public class CreateSprintTests : SprintFixture
{
    [Test]
    public async Task CreateSprint_ShouldCopyLastLength()
    {
        await using var connection = await OpenDatabaseAsync();
        var last = await connection.QuerySingleAsync<(string Name, DateOnly Start, DateOnly End)>(
            "SELECT s.name, s.start_date, s.end_date FROM sprint s JOIN project p ON p.id = s.project_id WHERE p.key = 'SHIP' ORDER BY s.end_date DESC LIMIT 1");
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);

        var reply = await CallToolAsync(token, "create_sprint", """{"project":"SHIP","goal":"Ship the thing"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        result.GetProperty("sprint").GetString().ShouldBe("Sprint 27");
        result.GetProperty("start_date").GetString().ShouldBe(last.End.AddDays(1).ToString("yyyy-MM-dd"));
        result.GetProperty("length_days").GetInt32().ShouldBe(last.End.DayNumber - last.Start.DayNumber + 1);
        result.GetProperty("based_on").GetString().ShouldBe(last.Name);
        var stored = await connection.QuerySingleAsync<(DateOnly Start, DateOnly End, string Goal)>(
            "SELECT start_date, end_date, goal FROM sprint WHERE name = 'Sprint 27' AND project_id = @project", new { project = SeedIds.Projects.Ship });
        stored.ShouldBe((last.End.AddDays(1), last.End.AddDays(1 + last.End.DayNumber - last.Start.DayNumber), "Ship the thing"));
    }

    [Test]
    public async Task CreateSprint_WhenWriteTokenOnly_ShouldAnswer403()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "create_sprint", """{"project":"SHIP"}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
