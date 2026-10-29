using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// One seeded scenario for the sprint write endpoints. Each test works on sprints of its own far in the future.
[TestFixture]
public class SprintApiTests : ApiFixture
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    private ProjectEventWatch Watch() => new(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Ship);

    private async Task<SprintDto> CreateAsync(string name, int startOffset)
    {
        using var response = await Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Ship}/sprints", new SprintCreate(name, Today.AddDays(startOffset), Today.AddDays(startOffset + 13), "Ship it"));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadAsync<SprintDto>();
    }

    [Test]
    public async Task Create_WhenListedAfterwards_ShouldMatchTheShellsSprintDto()
    {
        using var watch = Watch();

        var created = await CreateAsync("Listed sprint", 500);

        var listed = await Api.GetAsync<List<SprintDto>>($"api/projects/{SeedIds.Projects.Ship}/sprints");
        listed.Single(s => s.Id == created.Id).ShouldBe(created);
        created.Status.ShouldBe(SprintStatus.Future);
        created.RolloverStatus.ShouldBeNull();
        (await watch.NextAsync<SprintChanged>(e => e.SprintId == created.Id)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Create_WhenOverlappingOrNameTaken_ShouldBeRefused()
    {
        var first = await CreateAsync("Taken sprint", 600);

        using var overlapping = await Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Ship}/sprints", new SprintCreate("Overlapping", Today.AddDays(605), Today.AddDays(612), ""));
        using var duplicate = await Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Ship}/sprints", new SprintCreate(first.Name, Today.AddDays(700), Today.AddDays(713), ""));

        (await ApiClient.ReadApiErrorAsync(overlapping, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ApiClient.ReadApiErrorAsync(duplicate, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Sprint_WhenUpdated_ShouldPublishSprintChanged()
    {
        var created = await CreateAsync("Update sprint", 800);
        using var watch = Watch();

        using var response = await Api.PatchJsonAsync($"api/sprints/{created.Id}", new SprintUpdate("Renamed sprint", null, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var updated = await response.ReadAsync<SprintDto>();
        updated.Name.ShouldBe("Renamed sprint");
        updated.Goal.ShouldBe("Ship it");
        var changed = await watch.NextAsync<SprintChanged>(e => e.SprintId == created.Id);
        changed.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        changed.ClientId.ShouldBe(ApiClient.ClientId);
    }

    [Test]
    public async Task Sprint_WhenEndedAndItsDatesChange_ShouldBeRefusedButRenameWorks()
    {
        var ended = await CreateAsync("Long ended sprint", -3000);

        using var moved = await Api.PatchJsonAsync($"api/sprints/{ended.Id}", new SprintUpdate(null, null, ended.EndDate.AddDays(1), null));
        using var renamed = await Api.PatchJsonAsync($"api/sprints/{ended.Id}", new SprintUpdate("Long ended sprint, renamed", ended.StartDate, ended.EndDate, null));

        var error = await ApiClient.ReadApiErrorAsync(moved, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldBe("An ended sprint's dates can't change.");
        renamed.StatusCode.ShouldBe(HttpStatusCode.OK, await renamed.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Capacity_WhenPut_ShouldComputePointsRoundTripAndPublishSprintChanged()
    {
        var created = await CreateAsync("Capacity sprint", 900);
        using var watch = Watch();

        using var response = await Api.PutJsonAsync($"api/sprints/{created.Id}/capacity", new SprintCapacityPut([new MemberCapacityPut(SeedIds.Users.Priya, null, "half day"), new MemberCapacityPut(SeedIds.Users.Tom, 5, null)], 30));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var read = await Api.GetAsync<SprintCapacityDto>($"api/sprints/{created.Id}/capacity");
        read.Velocity.ShouldBe(30m);
        read.WorkingDays.ShouldBe(10);
        read.TeamTotal.ShouldBe(22.5m);
        read.Members.Single(m => m.User.Id == SeedIds.Users.Priya).Points.ShouldBe(15m);
        read.Members.Single(m => m.User.Id == SeedIds.Users.Priya).Note.ShouldBe("half day");
        read.Members.Single(m => m.User.Id == SeedIds.Users.Tom).Days.ShouldBe(5m);
        read.Members.Single(m => m.User.Id == SeedIds.Users.Tom).Points.ShouldBe(7.5m);
        read.Ended.ShouldBeFalse();
        (await watch.NextAsync<SprintChanged>(e => e.SprintId == created.Id)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Capacity_WhenProjectCountryHasHolidaysInTheSprint_ShouldLowerTheDefaultDays()
    {
        var created = await CreateAsync("Holiday sprint", 1000);
        var days = Enumerable.Range(0, 14).Select(created.StartDate.AddDays).ToList();
        var weekday = days.First(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        var weekend = days.First(d => d.DayOfWeek is DayOfWeek.Sunday);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("INSERT INTO holiday_country (code, name) VALUES ('NZ', 'New Zealand') ON CONFLICT (code) DO NOTHING");
        await connection.ExecuteAsync(
            "INSERT INTO public_holiday (country_code, date, name) VALUES ('NZ', @weekday, 'Weekday holiday'), ('NZ', @weekend, 'Sunday holiday') ON CONFLICT DO NOTHING", new { weekday, weekend });
        await connection.ExecuteAsync("UPDATE project SET holiday_country = 'NZ' WHERE id = @id", new { id = SeedIds.Projects.Ship });
        try
        {
            using var response = await Api.PutJsonAsync($"api/sprints/{created.Id}/capacity", new SprintCapacityPut([new MemberCapacityPut(SeedIds.Users.Priya, null, null)], 20));

            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var read = await response.ReadAsync<SprintCapacityDto>();
            read.HolidayCountry.ShouldBe("NZ");
            read.Holidays.ShouldBe([new ShipLean.Contracts.Holidays.PublicHolidayDto(weekday, "Weekday holiday")]);
            read.WorkingDays.ShouldBe(10);
            read.DefaultDays.ShouldBe(9);
            read.Members.Single().Points.ShouldBe(18m);
        }
        finally
        {
            await connection.ExecuteAsync("UPDATE project SET holiday_country = NULL WHERE id = @id", new { id = SeedIds.Projects.Ship });
        }
    }

    [Test]
    public async Task Capacity_WhenSprintEnded_ShouldRefuseThePut()
    {
        using var response = await Api.PutJsonAsync($"api/sprints/{SeedIds.Sprints.Sprint19}/capacity", new SprintCapacityPut([new MemberCapacityPut(SeedIds.Users.Priya, null, null)], 30));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldBe("An ended sprint's capacity can't change.");
    }

    [Test]
    public async Task Forecast_WhenOverrideSet_ShouldPublishProjectChanged()
    {
        using var watch = Watch();

        using var set = await Api.PutJsonAsync($"api/projects/{SeedIds.Projects.Ship}/forecast", new VelocityOverridePut(42.5m));

        set.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var changed = await watch.NextAsync<ProjectChanged>(_ => true);
        changed.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        changed.ClientId.ShouldBe(ApiClient.ClientId);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<decimal?>("SELECT velocity_override FROM project WHERE id = @id", new { id = SeedIds.Projects.Ship })).ShouldBe(42.5m);
        using var cleared = await Api.PutJsonAsync($"api/projects/{SeedIds.Projects.Ship}/forecast", new VelocityOverridePut(null));
        cleared.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await connection.ExecuteScalarAsync<decimal?>("SELECT velocity_override FROM project WHERE id = @id", new { id = SeedIds.Projects.Ship })).ShouldBeNull();
    }

    [Test]
    public async Task Forecast_WhenNegative_ShouldBeValidation()
    {
        using var response = await Api.PutJsonAsync($"api/projects/{SeedIds.Projects.Ship}/forecast", new VelocityOverridePut(-1));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
