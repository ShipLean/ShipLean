using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Projects.Api;

// The instance limits project creation to admins. Its own scenario because it changes an instance setting.
[TestFixture]
public class ProjectCreationLimitedApiTests : ApiFixture
{
    [OneTimeSetUp]
    public async Task LimitToAdmins()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE instance_settings SET project_creation = 'admins'");
    }

    [Test]
    public async Task Create_WhenCreationLimitedToAdmins_ShouldBeForbidden()
    {
        var request = new ProjectCreate("Limited", "LIMA", null, [], [1, 2, 3, 4, 5], null);

        using var refused = await Api.ActingAs(TestUser.Priya).PostJsonAsync("api/projects", request);
        using var allowed = await Api.PostJsonAsync("api/projects", request);

        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
    }
}
