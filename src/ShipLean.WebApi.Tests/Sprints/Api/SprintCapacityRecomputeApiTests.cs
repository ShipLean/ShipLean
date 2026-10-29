using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.ProjectSettings;
using ShipLean.WebApi.Tests.Holidays;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// The inputs of a sprint's default days change (its dates, the project's working days and country, the country's holidays) and
// the stored capacity follows. Runs the real IHolidaysChanged over the stub feed (which lists NZ, AU and CA); each test arranges
// projects of its own. A running sprint: velocity 20, two members, so a member's points are 10 * days / W.
[TestFixture]
public class SprintCapacityRecomputeApiTests : HolidayFeedFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    // A Monday at least a week ahead: a two-week sprint from it has 10 working days.
    private static DateOnly Start => Today.AddDays(7).AddDays((8 - (int)Today.AddDays(7).DayOfWeek) % 7);

    // The holiday: a Wednesday inside the first week.
    private static DateOnly Holiday => Start.AddDays(2);

    // Not the Real one: the fixture's recording double would hide the recompute.
    protected override Action<IServiceCollection>? ConfigureTestServices => null;

    [OneTimeSetUp]
    public async Task ListCountriesAsync()
    {
        FeedCountries(("NZ", "New Zealand"), ("AU", "Australia"), ("CA", "Canada"));
        await Refresher.RefreshCountriesAsync(CancellationToken.None);
    }

    [Test]
    public async Task EditDates_WhenSprintShortened_ShouldRecomputeDefaultDaysAndClampTypedDays()
    {
        var project = await ArrangeAsync("NZ");
        await StoreHolidayAsync("NZ", Holiday);
        var sprint = await ArrangeSprintAsync(project, "Running", Start, Start.AddDays(13), 20, (SeedIds.Users.Priya, null, 9m), (SeedIds.Users.Tom, 8m, 8m));
        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), project.Id);

        using var response = await Api.PatchJsonAsync($"api/sprints/{sprint}", new SprintUpdate(null, null, Start.AddDays(6), null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await watch.NextAsync<SprintChanged>(e => e.SprintId == sprint)).ProjectId.ShouldBe(project.Id);
        // W = 5, one holiday: Priya's default is 4 days (8 points); Tom's typed 8 is clamped to 5 (10 points).
        (await StoredAsync(sprint)).ShouldBe([(SeedIds.Users.Priya, null, 8m), (SeedIds.Users.Tom, 5m, 10m)], ignoreOrder: true);
    }

    [Test]
    public async Task EditDates_WhenOnlyTheNameChanges_ShouldLeaveStoredCapacityAlone()
    {
        var project = await ArrangeAsync(null);
        var sprint = await ArrangeSprintAsync(project, "Running", Start, Start.AddDays(13), 20, (SeedIds.Users.Priya, null, 1m));

        using var response = await Api.PatchJsonAsync($"api/sprints/{sprint}", new SprintUpdate("Renamed", null, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await StoredAsync(sprint)).ShouldBe([(SeedIds.Users.Priya, null, 1m)]);
    }

    [Test]
    public async Task Settings_WhenWorkingDaysChange_ShouldRecomputeRunningSprints()
    {
        var project = await ArrangeAsync(null);
        var sprint = await ArrangeSprintAsync(project, "Running", Start, Start.AddDays(13), 20, (SeedIds.Users.Priya, null, 10m), (SeedIds.Users.Tom, 10m, 10m));
        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), project.Id);

        // Monday to Thursday: W = 8. Tom's typed 10 clamps to 8.
        using var response = await SaveAsync(project, [1, 2, 3, 4], null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await watch.NextAsync<SprintChanged>(e => e.SprintId == sprint)).ProjectId.ShouldBe(project.Id);
        (await StoredAsync(sprint)).ShouldBe([(SeedIds.Users.Priya, null, 10m), (SeedIds.Users.Tom, 8m, 10m)], ignoreOrder: true);
    }

    [Test]
    public async Task Settings_WhenCountryChanges_ShouldRecomputeFromItsHolidays()
    {
        var project = await ArrangeAsync(null);
        await StoreHolidayAsync("NZ", Holiday);
        var sprint = await ArrangeSprintAsync(project, "Running", Start, Start.AddDays(13), 20, (SeedIds.Users.Priya, null, 10m), (SeedIds.Users.Tom, null, 10m));

        using var response = await SaveAsync(project, [1, 2, 3, 4, 5], "NZ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        // The default is 9 of 10 days: 9 points each.
        (await StoredAsync(sprint)).ShouldBe([(SeedIds.Users.Priya, null, 9m), (SeedIds.Users.Tom, null, 9m)], ignoreOrder: true);
    }

    [Test]
    public async Task Settings_WhenNeitherInputChanges_ShouldLeaveStoredCapacityAlone()
    {
        var project = await ArrangeAsync(null);
        var sprint = await ArrangeSprintAsync(project, "Running", Start, Start.AddDays(13), 20, (SeedIds.Users.Priya, null, 1m));

        using var response = await SaveAsync(project, [1, 2, 3, 4, 5], null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await StoredAsync(sprint)).ShouldBe([(SeedIds.Users.Priya, null, 1m)]);
    }

    [Test]
    public async Task Settings_WhenInputsChange_ShouldNeverTouchEndedOrNullVelocitySprints()
    {
        var project = await ArrangeAsync("NZ");
        await StoreHolidayAsync("NZ", Holiday);
        var ended = await ArrangeSprintAsync(project, "Ended", Today.AddDays(-60), Today.AddDays(-47), 20, (SeedIds.Users.Priya, 10m, 1m));
        var unplanned = await ArrangeSprintAsync(project, "No velocity", Start.AddDays(28), Start.AddDays(41), null, (SeedIds.Users.Priya, 10m, 1m));

        using var response = await SaveAsync(project, [1, 2], "AU");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await StoredAsync(ended)).ShouldBe([(SeedIds.Users.Priya, 10m, 1m)]);
        (await StoredAsync(unplanned)).ShouldBe([(SeedIds.Users.Priya, 10m, 1m)]);
    }

    [Test]
    public async Task HolidayRefresh_WhenCountryGainsADate_ShouldRecomputeOnlyProjectsUsingIt()
    {
        var ca = await ArrangeAsync("CA");
        var other = await ArrangeAsync("NZ");
        var none = await ArrangeAsync(null);
        var caSprint = await ArrangeSprintAsync(ca, "Running", Start, Start.AddDays(13), 20, (SeedIds.Users.Priya, null, 10m), (SeedIds.Users.Tom, null, 10m));
        var otherSprint = await ArrangeSprintAsync(other, "Running", Start, Start.AddDays(13), 20, (SeedIds.Users.Priya, null, 10m));
        var noneSprint = await ArrangeSprintAsync(none, "Running", Start, Start.AddDays(13), 20, (SeedIds.Users.Priya, null, 10m));
        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), ca.Id);
        foreach (var year in new[] { Today.Year, Today.Year + 1 })
        {
            FeedHolidays(year, "CA", Holiday.Year == year ? [($"{Holiday:yyyy-MM-dd}", "Test Day", true)] : []);
        }

        var changed = await Refresher.RefreshCountryAsync("CA", CancellationToken.None);

        changed.ShouldBeTrue();
        (await watch.NextAsync<SprintChanged>(e => e.SprintId == caSprint)).ProjectId.ShouldBe(ca.Id);
        (await StoredAsync(caSprint)).ShouldBe([(SeedIds.Users.Priya, null, 9m), (SeedIds.Users.Tom, null, 9m)], ignoreOrder: true);
        (await StoredAsync(otherSprint)).ShouldBe([(SeedIds.Users.Priya, null, 10m)]);
        (await StoredAsync(noneSprint)).ShouldBe([(SeedIds.Users.Priya, null, 10m)]);
    }

    private async Task<ArrangedProject> ArrangeAsync(string? country)
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Joel, ProjectRole.Admin));
        await connection.ExecuteAsync("UPDATE project SET holiday_country = @country WHERE id = @id", new { country, id = project.Id });
        return project;
    }

    private async Task StoreHolidayAsync(string country, DateOnly date)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("INSERT INTO public_holiday (country_code, date, name) VALUES (@country, @date, 'Test Day') ON CONFLICT DO NOTHING", new { country, date });
    }

    private async Task<Guid> ArrangeSprintAsync(ArrangedProject project, string name, DateOnly start, DateOnly end, decimal? velocity, params (Guid UserId, decimal? Days, decimal Points)[] rows)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date, goal, capacity_velocity) VALUES (@id, @project, @name, @start, @end, '', @velocity)",
            new { id, project = project.Id, name, start, end, velocity });
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(
                "INSERT INTO sprint_capacity (id, sprint_id, user_id, capacity_points, days) VALUES (@id, @sprint, @user, @points, @days)",
                new { id = Guid.NewGuid(), sprint = id, user = row.UserId, points = row.Points, row.Days });
        }

        return id;
    }

    private async Task<List<(Guid UserId, decimal? Days, decimal Points)>> StoredAsync(Guid sprint)
    {
        await using var connection = await OpenDatabaseAsync();
        return (await connection.QueryAsync<(Guid, decimal?, decimal)>(
            "SELECT user_id, days, capacity_points FROM sprint_capacity WHERE sprint_id = @sprint AND archived_at IS NULL", new { sprint })).ToList();
    }

    private async Task<HttpResponseMessage> SaveAsync(ArrangedProject project, int[] workingDays, string? country)
    {
        var loaded = await Api.GetAsync<ProjectSettingsDto>($"api/projects/{project.Id}/settings");
        var general = new ProjectGeneralRequest(loaded.General.Key, loaded.General.Name, loaded.General.Description, workingDays) { HolidayCountry = country };
        var request = new ProjectSettingsRequest(general, [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))], loaded.Sprints, new StateChanges([], [], [], []));
        return await Api.PutJsonAsync($"api/projects/{project.Id}/settings", JsonSerializer.SerializeToElement(request, WireOptions));
    }
}
