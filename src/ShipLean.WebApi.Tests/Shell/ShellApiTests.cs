using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Preferences;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Shell;

// One seeded scenario for the shell's reads and the preferences. Each test uses its own preference keys.
[TestFixture]
public class ShellApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private async Task<T> ReadAsync<T>(string path)
    {
        using var response = await Api.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Wire))!;
    }

    [Test]
    public async Task Projects_WhenListed_ShouldBeRecentFirst()
    {
        (await Api.PutJsonAsync("api/me/preferences/recentProjects", new[] { SeedIds.Projects.Ops, SeedIds.Projects.Mob })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var projects = await ReadAsync<List<ProjectSummaryDto>>("api/projects");

        projects.Take(2).Select(p => p.Id).ShouldBe([SeedIds.Projects.Ops, SeedIds.Projects.Mob]);
        projects.Skip(2).Select(p => p.Name).ShouldBe(projects.Skip(2).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList());
        projects.Single(p => p.Id == SeedIds.Projects.Ship).Role.ShouldBe(ProjectRole.Admin);
        projects.Single(p => p.Id == SeedIds.Projects.Ops).Role.ShouldBe(ProjectRole.Member);
    }

    [Test]
    public async Task Projects_WhenListed_ShouldOmitProjectsImNotIn()
    {
        var projects = await ReadAsync<List<ProjectSummaryDto>>("api/projects");

        projects.Select(p => p.Id).ShouldNotContain(SeedIds.Projects.Part);
        projects.Select(p => p.Key).ShouldContain("SHIP");
    }

    [Test]
    public async Task Project_WhenKnownKey_ShouldCarryTheSettingsTheShellNeeds()
    {
        var byKey = await ReadAsync<ProjectDto>("api/projects/by-key/ship");
        var byId = await ReadAsync<ProjectDto>($"api/projects/{SeedIds.Projects.Ship}");

        byKey.Id.ShouldBe(byId.Id);
        byKey.NextItemNumber.ShouldBe(byId.NextItemNumber);
        byKey.Key.ShouldBe("SHIP");
        byKey.Role.ShouldBe(ProjectRole.Admin);
        byKey.WorkingDays.ShouldBe([1, 2, 3, 4, 5]);
        byKey.MemberCount.ShouldBeGreaterThan(1);
    }

    [Test]
    public async Task Project_WhenUnknownKey_ShouldBeNotFound()
    {
        using var response = await Api.GetAsync("api/projects/by-key/ZZZZ");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Project_WhenNotMemberByKey_ShouldBeNotFound()
    {
        using var response = await Api.GetAsync("api/projects/by-key/PART");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Sprints_WhenListed_ShouldCarryLabelsAndStatus()
    {
        var sprints = await ReadAsync<List<SprintDto>>($"api/projects/{SeedIds.Projects.Ship}/sprints");

        sprints.Select(s => s.StartDate).ShouldBe(sprints.Select(s => s.StartDate).Order().ToList());
        var current = sprints.Single(s => s.Id == SeedIds.Sprints.Sprint24);
        current.Status.ShouldBe(SprintStatus.Current);
        var today = SprintLabels.Today(TimeProvider.System);
        current.DatesLabel.ShouldBe(SprintLabels.Dates(current.StartDate, current.EndDate, today));
        current.TimeLabel.ShouldBe(SprintLabels.Time(current.StartDate, current.EndDate, today));
        sprints.Single(s => s.Id == SeedIds.Sprints.Sprint23).Status.ShouldBe(SprintStatus.Past);
        sprints.Single(s => s.Id == SeedIds.Sprints.Sprint23).RolloverStatus.ShouldBe(RolloverStatus.Decided);
        sprints.Single(s => s.Id == SeedIds.Sprints.Sprint25).Status.ShouldBe(SprintStatus.Future);
        sprints.Single(s => s.Id == SeedIds.Sprints.Sprint25).RolloverStatus.ShouldBeNull();
    }

    [Test]
    public async Task States_WhenListed_ShouldIncludeCustomStates()
    {
        var states = await ReadAsync<List<StateDto>>($"api/projects/{SeedIds.Projects.Ship}/states");

        states.ShouldContain(s => s.Type == WorkItemTypeId.Bug && s.Name == "Triaged");
        states.ShouldContain(s => s.Type == WorkItemTypeId.Task && s.Name == "In review");
        states.ShouldContain(s => s.Category == StateCategory.Removed);
        states.Select(s => s.Type).Distinct().ShouldBe([WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task]);
        foreach (var group in states.GroupBy(s => s.Type))
        {
            group.Select(s => s.Order).ShouldBe(group.Select(s => s.Order).Order().ToList());
        }
    }

    [Test]
    public async Task States_WhenListed_ShouldOmitArchivedStates()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await Dapper.SqlMapper.ExecuteAsync(connection,
                "INSERT INTO state (id, project_id, type, name, category, sort_order, archived_at) VALUES (@id, @project, 'story', 'Retired', 'inProgress', 90, now())",
                new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship });
        }

        var states = await ReadAsync<List<StateDto>>($"api/projects/{SeedIds.Projects.Ship}/states");

        states.ShouldNotContain(s => s.Name == "Retired");
        states.ShouldContain(s => s.Type == WorkItemTypeId.Story && s.Category == StateCategory.InProgress);
    }

    [Test]
    public async Task Members_WhenListed_ShouldCarryRoles()
    {
        var members = await ReadAsync<List<MemberDto>>($"api/projects/{SeedIds.Projects.Ship}/members");

        members.Single(m => m.User.Id == SeedIds.Users.Joel).Role.ShouldBe(ProjectRole.Admin);
        members.Single(m => m.User.Id == SeedIds.Users.Joel).Email.ShouldBe("joel@shiplean.local");
        members.Single(m => m.User.Id == SeedIds.Users.Priya).Role.ShouldBe(ProjectRole.Member);
    }

    [Test]
    public async Task Preferences_WhenPutThenGet_ShouldRoundTrip()
    {
        (await Api.GetAsync("api/me/preferences/density")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Api.PutJsonAsync("api/me/preferences/density", "comfortable")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Api.PutJsonAsync("api/me/preferences/view:backlog", new { filter = "mine", columns = new[] { "title", "state" } })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var density = await ReadAsync<UserPreferenceDto>("api/me/preferences/density");
        density.Key.ShouldBe("density");
        density.Json.GetString().ShouldBe("comfortable");
        var view = (await ReadAsync<UserPreferenceDto>("api/me/preferences/view:backlog")).Json;
        view.GetProperty("filter").GetString().ShouldBe("mine");
        view.GetProperty("columns").GetArrayLength().ShouldBe(2);
    }

    [Test]
    public async Task Preferences_WhenUnknownKey_ShouldBeValidation()
    {
        using var unknown = await Api.GetAsync("api/me/preferences/colour");
        using var invalidValue = await Api.PutJsonAsync("api/me/preferences/theme", "neon");
        using var invalidPut = await Api.PutJsonAsync("api/me/preferences/colour", "red");

        (await ApiClient.ReadApiErrorAsync(unknown, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ApiClient.ReadApiErrorAsync(invalidValue, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ApiClient.ReadApiErrorAsync(invalidPut, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task ResetViews_ShouldNullViewKeysOnly()
    {
        await Api.PutJsonAsync("api/me/preferences/view:board", new { swimlanes = true });
        await Api.PutJsonAsync("api/me/preferences/view:sprints", new { zoom = 2 });
        await Api.PutJsonAsync("api/me/preferences/theme", "dark");

        (await Api.PostJsonAsync("api/me/preferences/reset-views")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Api.GetAsync("api/me/preferences/view:board")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Api.GetAsync("api/me/preferences/view:sprints")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ReadAsync<UserPreferenceDto>("api/me/preferences/theme")).Json.GetString().ShouldBe("dark");
        await using var connection = await OpenDatabaseAsync();
        var rows = await Dapper.SqlMapper.QuerySingleAsync<int>(connection, "SELECT count(*)::int FROM user_preference WHERE key LIKE 'view:%'");
        rows.ShouldBeGreaterThanOrEqualTo(2);
    }
}
