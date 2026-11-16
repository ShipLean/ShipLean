using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.ProjectSettings;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.ProjectSettings.Api;

// The bug severity weights on a project's General settings (SHIP-1398 item D16): 8/4/2/1 by default, changed by a project admin,
// always all four severities with a positive whole weight. Joel is the project's admin, Priya a member.
[TestFixture]
public class SeverityWeightsTests : ApiFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private async Task<ArrangedProject> ArrangeAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Joel, ProjectRole.Admin), (SeedIds.Users.Priya, ProjectRole.Member));
    }

    private async Task<ProjectSettingsDto> LoadAsync(ArrangedProject project)
        => await Api.GetAsync<ProjectSettingsDto>($"api/projects/{project.Id}/settings");

    private static ProjectSettingsRequest Request(ProjectSettingsDto loaded, IReadOnlyDictionary<int, int>? weights)
        => new(
            new ProjectGeneralRequest(loaded.General.Key, loaded.General.Name, loaded.General.Description, loaded.General.WorkingDays) { BugSeverityWeights = weights },
            [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))],
            loaded.Sprints,
            new StateChanges([], [], [], []));

    private Task<HttpResponseMessage> PutAsync(ArrangedProject project, ProjectSettingsRequest request, ApiClient? api = null)
        => (api ?? Api).PutJsonAsync($"api/projects/{project.Id}/settings", JsonSerializer.SerializeToElement(request, WireOptions));

    private async Task<string> StoredAsync(ArrangedProject project)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT bug_severity_weights::text FROM project WHERE id = @id", new { id = project.Id });
    }

    [Test]
    public async Task Settings_WhenNothingChanged_ShouldReadTheDefaultWeights()
    {
        var project = await ArrangeAsync();

        (await LoadAsync(project)).General.BugSeverityWeights.ShouldBe(new Dictionary<int, int> { [1] = 8, [2] = 4, [3] = 2, [4] = 1 });
    }

    [Test]
    public async Task Update_WhenAdminChangesWeights_ShouldStoreAndReadThemBack()
    {
        var project = await ArrangeAsync();
        var weights = new Dictionary<int, int> { [1] = 10, [2] = 5, [3] = 3, [4] = 1 };

        using var response = await PutAsync(project, Request(await LoadAsync(project), weights));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await LoadAsync(project)).General.BugSeverityWeights.ShouldBe(weights);
        (await StoredAsync(project)).ShouldBe("""{"1": 10, "2": 5, "3": 3, "4": 1}""");
    }

    [Test]
    public async Task Update_WhenWeightsLeftOut_ShouldKeepTheSavedOnes()
    {
        var project = await ArrangeAsync();
        var weights = new Dictionary<int, int> { [1] = 20, [2] = 10, [3] = 5, [4] = 2 };
        using var saved = await PutAsync(project, Request(await LoadAsync(project), weights));
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await PutAsync(project, Request(await LoadAsync(project), null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await LoadAsync(project)).General.BugSeverityWeights.ShouldBe(weights);
    }

    [Test]
    public async Task Update_WhenNotProjectAdmin_ShouldBeRefused()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);

        using var response = await PutAsync(project, Request(loaded, new Dictionary<int, int> { [1] = 9, [2] = 9, [3] = 9, [4] = 9 }), Api.ActingAs(TestUser.Priya));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        (await LoadAsync(project)).General.BugSeverityWeights[1].ShouldBe(8);
    }

    [TestCase(0)]
    [TestCase(-3)]
    public async Task Update_WhenWeightNotPositive_ShouldReturnValidation(int weight)
    {
        var project = await ArrangeAsync();

        using var response = await PutAsync(project, Request(await LoadAsync(project), new Dictionary<int, int> { [1] = 8, [2] = weight, [3] = 2, [4] = 1 }));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("bugSeverityWeights");
        (await StoredAsync(project)).ShouldBe("""{"1": 8, "2": 4, "3": 2, "4": 1}""");
    }

    [TestCase("1,2,3")]
    [TestCase("1,2,3,4,5")]
    [TestCase("0,1,2,3")]
    public async Task Update_WhenKeysAreNotExactlyOneToFour_ShouldReturnValidation(string severities)
    {
        var project = await ArrangeAsync();
        var weights = severities.Split(',').Select(int.Parse).ToDictionary(s => s, _ => 3);

        using var response = await PutAsync(project, Request(await LoadAsync(project), weights));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("bugSeverityWeights");
    }
}
