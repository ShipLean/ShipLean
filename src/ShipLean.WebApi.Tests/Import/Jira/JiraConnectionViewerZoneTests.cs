using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Import;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;
using ShipLean.TestSupport.Jira;

namespace ShipLean.WebApi.Tests.Import.Jira;

// The Jira connection's created label in the viewer's zone. Each test acts as a viewer of its own who administers SHIP.
[TestFixture]
public sealed class JiraConnectionViewerZoneTests : ApiFixture
{
    private static readonly string Connection = $"api/projects/{SeedIds.Projects.Ship}/jira/connection";

    private readonly JiraStubHandler _stub = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => JiraStubHost.Pin(services, _stub);

    // A viewer of its own, an admin of the project: the seeded users' zones are never touched.
    private async Task<TestUser> ArrangeViewerAsync(string? zone)
    {
        TestUser viewer;
        await using (var connection = await OpenDatabaseAsync())
        {
            var id = await new AppUserBuilder().InsertAsync(connection);
            await connection.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @project, @user, 'admin')",
                new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, user = id });
            viewer = TestUser.Of(id);
        }

        if (zone is not null)
        {
            await ViewerZoneArrange.SetAsync(Api, viewer, zone);
        }

        return viewer;
    }

    private async Task<string> CreatedLabelAsync(TestUser viewer)
    {
        var body = new { baseUrl = JiraStubHandler.CloudUrl, kind = "cloud", email = JiraStubHandler.CloudEmail, token = JiraStubHandler.CloudToken };
        using var saved = await Api.ActingAs(viewer).PutJsonAsync(Connection, body);
        saved.EnsureSuccessStatusCode();
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE jira_connection SET created_at = @at WHERE project_id = @project", new { at = ViewerZoneTestData.LateEveningUtc, project = SeedIds.Projects.Ship });
        }

        using var read = await Api.ActingAs(viewer).GetAsync(Connection);
        read.EnsureSuccessStatusCode();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        return JsonSerializer.Deserialize<JiraConnectionDto>(await read.Content.ReadAsStringAsync(), options)!.CreatedLabel;
    }

    [Test]
    public async Task JiraConnections_WhenViewerHasNoZone_ShouldDateTheConnectionByTheUtcDay()
    {
        (await CreatedLabelAsync(await ArrangeViewerAsync(null))).ShouldBe(ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture));
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to the CreatedLabel Relative call in JiraConnectionService.
    [Test]
    public async Task JiraConnections_WhenViewerIsInBangkok_ShouldDateTheConnectionByTheBangkokDay()
    {
        var viewer = await ArrangeViewerAsync(ViewerZoneTestData.BangkokId);

        var label = await CreatedLabelAsync(viewer);

        var bangkokDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.FromHours(7)).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var utcDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        bangkokDay.ShouldNotBe(utcDay);
        label.ShouldBe(bangkokDay);
    }
}
