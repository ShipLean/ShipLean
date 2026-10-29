using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.ProjectSettings;
using ShipLean.WebApi.Tests.Holidays;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.ProjectSettings.Api;

// The holiday country on a project's General settings, over the stub holiday feed: the feed lists NZ and AU. Every test arranges a
// project of its own; Joel is its admin.
[TestFixture]
public class ProjectHolidayCountryApiTests : HolidayFeedFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [OneTimeSetUp]
    public async Task ListCountriesAsync()
    {
        FeedCountries(("NZ", "New Zealand"), ("AU", "Australia"));
        await Refresher.RefreshCountriesAsync(CancellationToken.None);
    }

    private async Task<ArrangedProject> ArrangeAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Joel, ProjectRole.Admin));
    }

    private Task<ProjectSettingsDto> LoadAsync(ArrangedProject project) => Api.GetAsync<ProjectSettingsDto>($"api/projects/{project.Id}/settings");

    private static ProjectSettingsRequest Request(ProjectSettingsDto loaded, ProjectGeneralRequest general)
        => new(general, [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))], loaded.Sprints, new StateChanges([], [], [], []));

    private static ProjectGeneralRequest General(ProjectSettingsDto loaded, string? country)
        => new(loaded.General.Key, loaded.General.Name, loaded.General.Description, loaded.General.WorkingDays) { HolidayCountry = country };

    private Task<HttpResponseMessage> PutAsync(ArrangedProject project, ProjectSettingsRequest request)
        => Api.PutJsonAsync($"api/projects/{project.Id}/settings", JsonSerializer.SerializeToElement(request, WireOptions));

    // The body as an older client sends it: no holidayCountry field at all.
    private async Task<HttpResponseMessage> PutWithoutCountryAsync(ArrangedProject project, ProjectSettingsDto loaded)
    {
        var body = JsonSerializer.SerializeToNode(Request(loaded, General(loaded, null)), WireOptions)!;
        body["general"]!.AsObject().Remove("holidayCountry");
        return await Api.PutJsonAsync($"api/projects/{project.Id}/settings", JsonSerializer.SerializeToElement(body));
    }

    private async Task<string?> StoredCountryAsync(ArrangedProject project)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string?>("SELECT holiday_country::text FROM project WHERE id = @id", new { id = project.Id });
    }

    private async Task<bool> TrackedAsync(string code)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<bool>("SELECT tracked FROM holiday_country WHERE code = @code", new { code });
    }

    [Test]
    public async Task Settings_WhenNewProject_ShouldHaveNoCountry()
    {
        var project = await ArrangeAsync();

        (await LoadAsync(project)).General.HolidayCountry.ShouldBeNull();
    }

    [Test]
    public async Task Settings_WhenCountryPicked_ShouldRoundTripAndTrackIt()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);

        using var response = await PutAsync(project, Request(loaded, General(loaded, "nz")));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoadAsync(project)).General.HolidayCountry.ShouldBe("NZ");
        (await StoredCountryAsync(project)).ShouldBe("NZ");
        (await TrackedAsync("NZ")).ShouldBeTrue();
        (await Api.GetAsync<ProjectDto>($"api/projects/{project.Id}")).HolidayCountry.ShouldBe("NZ");
    }

    [Test]
    public async Task Settings_WhenCountryUnknown_ShouldBeRefusedAndKeepTheSaved()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);
        (await PutAsync(project, Request(loaded, General(loaded, "AU")))).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await PutAsync(project, Request(loaded, General(loaded, "ZZ")));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await StoredCountryAsync(project)).ShouldBe("AU");
    }

    [Test]
    public async Task Settings_WhenCountryNullOrBlank_ShouldClearToNone()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);
        (await PutAsync(project, Request(loaded, General(loaded, "NZ")))).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await PutAsync(project, Request(loaded, General(loaded, null)))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredCountryAsync(project)).ShouldBeNull();

        (await PutAsync(project, Request(loaded, General(loaded, "AU")))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PutAsync(project, Request(loaded, General(loaded, "  ")))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredCountryAsync(project)).ShouldBeNull();
    }

    [Test]
    public async Task Settings_WhenCountryOmitted_ShouldKeepTheSaved()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);
        (await PutAsync(project, Request(loaded, General(loaded, "NZ")))).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await PutWithoutCountryAsync(project, loaded);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredCountryAsync(project)).ShouldBe("NZ");
    }

    [Test]
    public async Task Create_WhenCopiedFromProjectWithCountry_ShouldCopyTheCountry()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);
        (await PutAsync(project, Request(loaded, General(loaded, "AU")))).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await Api.PostJsonAsync("api/projects", new ProjectCreate("Copied country", "ZHC", project.Id, [], [1, 2, 3, 4, 5], null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var copy = await response.ReadAsync<ProjectDto>();
        copy.HolidayCountry.ShouldBe("AU");
        (await Api.GetAsync<ProjectSettingsDto>($"api/projects/{copy.Id}/settings")).General.HolidayCountry.ShouldBe("AU");
    }
}
