using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Instance;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Instance;

// One seeded scenario for the instance admin endpoints. Joel is the instance admin; each join test uses a project of its own.
[TestFixture]
public class InstanceApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    private async Task<(string Role, bool Archived)?> MembershipAsync(Guid projectId, Guid userId)
    {
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleOrDefaultAsync<(string Role, DateTime? ArchivedAt)?>(
            "SELECT role, archived_at FROM project_member WHERE project_id = @projectId AND user_id = @userId", new { projectId, userId });
        return row is { } found ? (found.Role, found.ArchivedAt is not null) : null;
    }

    [Test]
    public async Task InstanceSettings_WhenNotInstanceAdmin_ShouldBeForbidden()
    {
        var mei = Api.ActingAs(TestUser.Mei);

        using var read = await mei.GetAsync("api/instance/settings");
        using var write = await mei.PutJsonAsync("api/instance/settings", new InstanceSettingsDto("Mine", "https://mine.example.com", ProjectCreation.Admins));
        using var list = await mei.GetAsync("api/instance/projects");
        using var join = await mei.PostJsonAsync($"api/instance/projects/{SeedIds.Projects.Part}/join");

        foreach (var response in new[] { read, write, list, join })
        {
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        }
        (await MembershipAsync(SeedIds.Projects.Part, SeedIds.Users.Mei)).ShouldBeNull();
    }

    [Test]
    public async Task InstanceSettings_WhenSaved_ShouldRoundTrip()
    {
        using var saved = await Api.PutJsonAsync("api/instance/settings", new { name = "  Round trip  ", baseUrl = "https://round.example.com/", projectCreation = "admins" });

        (await ReadAsync<InstanceSettingsDto>(saved)).ShouldBe(new InstanceSettingsDto("Round trip", "https://round.example.com", ProjectCreation.Admins));
        var read = await ReadAsync<InstanceSettingsDto>(await Api.GetAsync("api/instance/settings"));
        read.ShouldBe(new InstanceSettingsDto("Round trip", "https://round.example.com", ProjectCreation.Admins));
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM instance_settings")).ShouldBe(1);
    }

    [TestCase("")]
    [TestCase("not a url")]
    [TestCase("ftp://files.example.com")]
    [TestCase("https://example.com/app")]
    [TestCase("https://example.com/?x=1")]
    [TestCase("https://user:pw@example.com")]
    public async Task InstanceSettings_WhenBaseUrlInvalid_ShouldBeValidation(string baseUrl)
    {
        using var response = await Api.PutJsonAsync("api/instance/settings", new { name = "Name", baseUrl, projectCreation = "everyone" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task InstanceSettings_WhenNameBlank_ShouldBeValidation()
    {
        using var response = await Api.PutJsonAsync("api/instance/settings", new { name = "   ", baseUrl = "https://example.com", projectCreation = "everyone" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task InstanceSettings_WhenProjectCreationUnknown_ShouldBeRefused()
    {
        using var response = await Api.PutJsonAsync("api/instance/settings", new { name = "Name", baseUrl = "https://example.com", projectCreation = "nobody" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test, Order(1)]
    public async Task InstanceProjects_ShouldFlagProjectWithNoActiveAdmin()
    {
        var projects = await ReadAsync<List<InstanceProjectDto>>(await Api.GetAsync("api/instance/projects"));

        projects.Count.ShouldBe(8);
        var arch = projects.Single(p => p.Id == SeedIds.Projects.Arch);
        arch.HasActiveAdmin.ShouldBeFalse();
        arch.Admins.ShouldNotBeEmpty();
        var ship = projects.Single(p => p.Id == SeedIds.Projects.Ship);
        ship.HasActiveAdmin.ShouldBeTrue();
        ship.Admins.Select(a => a.Id).ShouldBe([SeedIds.Users.Joel]);
        ship.MemberCount.ShouldBe(6);
        ship.ItemCount.ShouldBeGreaterThan(0);
        ship.YourRole.ShouldBe(ProjectRole.Admin);
        projects.Single(p => p.Id == SeedIds.Projects.Pay).YourRole.ShouldBe(ProjectRole.Member);
        projects.Single(p => p.Id == SeedIds.Projects.Part).YourRole.ShouldBeNull();
    }

    [Test]
    public async Task Join_WhenInstanceAdmin_ShouldAddAsAdminMember()
    {
        (await MembershipAsync(SeedIds.Projects.Part, SeedIds.Users.Joel)).ShouldBeNull();

        using var response = await Api.PostJsonAsync($"api/instance/projects/{SeedIds.Projects.Part}/join");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await MembershipAsync(SeedIds.Projects.Part, SeedIds.Users.Joel)).ShouldBe(("admin", false));
        var projects = await ReadAsync<List<InstanceProjectDto>>(await Api.GetAsync("api/instance/projects"));
        projects.Single(p => p.Id == SeedIds.Projects.Part).YourRole.ShouldBe(ProjectRole.Admin);
    }

    [Test]
    public async Task Join_WhenAlreadyMember_ShouldPromoteToAdmin()
    {
        (await MembershipAsync(SeedIds.Projects.Pay, SeedIds.Users.Joel)).ShouldBe(("member", false));

        using var response = await Api.PostJsonAsync($"api/instance/projects/{SeedIds.Projects.Pay}/join");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await MembershipAsync(SeedIds.Projects.Pay, SeedIds.Users.Joel)).ShouldBe(("admin", false));
    }

    [Test]
    public async Task Join_WhenMembershipArchived_ShouldRestoreAsAdmin()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            var changed = await connection.ExecuteAsync(
                "UPDATE project_member SET role = 'member', archived_at = now() WHERE project_id = @project AND user_id = @user",
                new { project = SeedIds.Projects.Data, user = SeedIds.Users.Joel });
            changed.ShouldBe(1);
        }

        using var response = await Api.PostJsonAsync($"api/instance/projects/{SeedIds.Projects.Data}/join");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await MembershipAsync(SeedIds.Projects.Data, SeedIds.Users.Joel)).ShouldBe(("admin", false));
    }

    [Test]
    public async Task Join_WhenUnknownProject_ShouldBeNotFound()
    {
        using var response = await Api.PostJsonAsync($"api/instance/projects/{Guid.NewGuid()}/join");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
