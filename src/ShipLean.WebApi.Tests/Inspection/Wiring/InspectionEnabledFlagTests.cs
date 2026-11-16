using ShipLean.WebApi.Tests.Taskboard;
using System.Net;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// The work item read says whether the page shows Agent runs: true only with the instance and the project on.
[TestFixture]
public class InspectionEnabledFlagTests : InspectionApiFixture
{
    private static readonly string ByKey = $"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{Item}";

    [Test]
    [Order(1)]
    public async Task Read_WhenProjectOff_ShouldBeFalse()
    {
        var item = await Api.GetAsync<WorkItemDto>(ByKey);

        item.InspectionEnabled.ShouldBeFalse();
    }

    [Test]
    [Order(2)]
    public async Task Read_WhenProjectOn_ShouldBeTrue()
    {
        using var put = await TurnOnAsync();
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var item = await Api.GetAsync<WorkItemDto>(ByKey);

        item.InspectionEnabled.ShouldBeTrue();
    }
}

// The instance switch off wins over a project row that says on.
[TestFixture]
public class InspectionEnabledFlagInstanceOffTests : ShipLean.TestSupport.Fixtures.ApiFixture
{
    [Test]
    public async Task Read_WhenInstanceOff_ShouldBeFalseEvenWithProjectRowOn()
    {
        await using var connection = await OpenDatabaseAsync();
        await Dapper.SqlMapper.ExecuteAsync(connection, "INSERT INTO inspection_project_settings (project_id, enabled) VALUES (@p, true)", new { p = SeedIds.Projects.Ship });

        var item = await Api.GetAsync<WorkItemDto>($"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/SHIP-124");

        item.InspectionEnabled.ShouldBeFalse();
    }
}
