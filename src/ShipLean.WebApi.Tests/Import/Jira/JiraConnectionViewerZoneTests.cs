using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Import;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;
using ShipLean.TestSupport.Jira;

namespace ShipLean.WebApi.Tests.Import.Jira;

// The Jira connection's created label in the viewer's zone. Joel is the admin of SHIP; the tests run in order: the fallback first, then the zone.
[TestFixture]
public sealed class JiraConnectionViewerZoneTests : ApiFixture
{
    private static readonly string Connection = $"api/projects/{SeedIds.Projects.Ship}/jira/connection";

    private readonly JiraStubHandler _stub = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => JiraStubHost.Pin(services, _stub);

    private async Task<string> CreatedLabelAsync()
    {
        var body = new { baseUrl = JiraStubHandler.CloudUrl, kind = "cloud", email = JiraStubHandler.CloudEmail, token = JiraStubHandler.CloudToken };
        using var saved = await Api.PutJsonAsync(Connection, body);
        saved.EnsureSuccessStatusCode();
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE jira_connection SET created_at = @at WHERE project_id = @project", new { at = ViewerZoneTestData.LateEveningUtc, project = SeedIds.Projects.Ship });
        }

        using var read = await Api.GetAsync(Connection);
        read.EnsureSuccessStatusCode();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        return JsonSerializer.Deserialize<JiraConnectionDto>(await read.Content.ReadAsStringAsync(), options)!.CreatedLabel;
    }

    [Test, Order(1)]
    public async Task JiraConnections_WhenViewerHasNoZone_ShouldDateTheConnectionByTheUtcDay()
    {
        (await CreatedLabelAsync()).ShouldBe(ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture));
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to the CreatedLabel Relative call in JiraConnectionService.
    [Test, Order(2)]
    public async Task JiraConnections_WhenViewerIsInBangkok_ShouldDateTheConnectionByTheBangkokDay()
    {
        await ViewerZoneArrange.SetAsync(Api, TestUser.Joel, ViewerZoneTestData.BangkokId);

        var label = await CreatedLabelAsync();

        var bangkokDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.FromHours(7)).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var utcDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        bangkokDay.ShouldNotBe(utcDay);
        label.ShouldBe(bangkokDay);
    }
}
