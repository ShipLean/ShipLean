using Dapper;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Reports;

// report_task in a project whose task states carry other names: it moves by category, never by name.
[TestFixture]
public class ReportCustomStateTests : ReportTestBase
{
    [OneTimeSetUp]
    public async Task RenameStatesAndSeed()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                """
                UPDATE state SET name = CASE category WHEN 'proposed' THEN 'Backlog' WHEN 'inProgress' THEN 'Doing' WHEN 'resolved' THEN 'Checking' WHEN 'completed' THEN 'Shipped' ELSE name END
                WHERE project_id = @project AND type = 'task'
                """,
                new { project = SeedIds.Projects.Ship });
        }
        await SeedPlanAsync();
    }

    [Test]
    public async Task ReportTask_CustomStateNames_ShouldMoveByCategory()
    {
        var done = NextTask();
        var blocked = NextTask();
        await PickUpAsync(blocked);
        var partial = NextTask();

        (await ReportAsync($$"""{"item":"{{done}}","status":"done"}""")).Structured().GetProperty("state").GetString().ShouldBe("Shipped");
        (await ReportAsync($$"""{"item":"{{blocked}}","status":"blocked","constraint_not_met":"No."}""")).Structured().GetProperty("state").GetString().ShouldBe("Backlog");
        (await ReportAsync($$"""{"item":"{{partial}}","status":"partial"}""")).Structured().GetProperty("state").GetString().ShouldBe("Doing");

        (await StateOfAsync(done)).ShouldBe("Shipped");
        (await StateOfAsync(blocked)).ShouldBe("Backlog");
        (await StateOfAsync(partial)).ShouldBe("Doing");
    }
}
