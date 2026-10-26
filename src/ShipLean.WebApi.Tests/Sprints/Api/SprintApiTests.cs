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
    public async Task Capacity_WhenPut_ShouldRoundTripAndPublishSprintChanged()
    {
        var created = await CreateAsync("Capacity sprint", 900);
        using var watch = Watch();

        using var response = await Api.PutJsonAsync($"api/sprints/{created.Id}/capacity", new SprintCapacityPut([new MemberCapacityPut(SeedIds.Users.Priya, 6, "half day"), new MemberCapacityPut(SeedIds.Users.Tom, 4.5m, null)]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var read = await Api.GetAsync<SprintCapacityDto>($"api/sprints/{created.Id}/capacity");
        read.TeamTotal.ShouldBe(10.5m);
        read.Members.Single(m => m.User.Id == SeedIds.Users.Priya).Note.ShouldBe("half day");
        (await watch.NextAsync<SprintChanged>(e => e.SprintId == created.Id)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
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
