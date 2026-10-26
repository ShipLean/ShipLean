using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Sprints.Defaults;

// The New sprint form's defaults on the seeded projects: SHIP has Sprints 19 to 26, Pay has none. The tests run in order:
// the create comes after the read of SHIP's defaults.
[TestFixture]
public class SprintDefaultsApiTests : ApiFixture
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    private static int IsoDay(DateOnly day) => day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek;

    [Test]
    [Order(1)]
    public async Task Next_WhenProjectHasSprints_ShouldFollowTheLastWithItsLength()
    {
        var sprints = await Api.GetAsync<List<SprintDto>>($"api/projects/{SeedIds.Projects.Ship}/sprints");
        var project = await Api.GetAsync<ProjectDto>($"api/projects/{SeedIds.Projects.Ship}");
        var last = sprints.Single(s => s.Name == "Sprint 26");

        var next = await Api.GetAsync<SprintCreate>($"api/projects/{SeedIds.Projects.Ship}/sprints/next");

        next.Name.ShouldBe("Sprint 27");
        next.StartDate.ShouldBeGreaterThan(last.EndDate);
        next.StartDate.ShouldBeLessThanOrEqualTo(last.EndDate.AddDays(7));
        project.WorkingDays.ShouldContain(IsoDay(next.StartDate));
        var lastLength = last.EndDate.DayNumber - last.StartDate.DayNumber;
        var length = next.EndDate.DayNumber - next.StartDate.DayNumber;
        length.ShouldBe(lastLength);
        next.Goal.ShouldBe(string.Empty);
    }

    [Test]
    [Order(2)]
    public async Task Next_WhenAcceptedAsIs_ShouldBeCreatable()
    {
        var next = await Api.GetAsync<SprintCreate>($"api/projects/{SeedIds.Projects.Ship}/sprints/next");

        using var created = await Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Ship}/sprints", next);

        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Next_WhenProjectHasNoSprints_ShouldStartOnAWorkingDayFromToday()
    {
        var project = await Api.GetAsync<ProjectDto>($"api/projects/{SeedIds.Projects.Pay}");
        (await Api.GetAsync<List<SprintDto>>($"api/projects/{SeedIds.Projects.Pay}/sprints")).ShouldBeEmpty();

        var next = await Api.GetAsync<SprintCreate>($"api/projects/{SeedIds.Projects.Pay}/sprints/next");

        next.Name.ShouldBe("Sprint 1");
        project.WorkingDays.ShouldContain(IsoDay(next.StartDate));
        next.StartDate.ShouldBeGreaterThanOrEqualTo(Today);
        next.StartDate.ShouldBeLessThanOrEqualTo(Today.AddDays(6));
        (next.EndDate.DayNumber - next.StartDate.DayNumber).ShouldBe(13);
    }

    [Test]
    public async Task Next_WhenUnknownProject_ShouldBeNotFound()
    {
        using var response = await Api.GetAsync($"api/projects/{Guid.NewGuid()}/sprints/next");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
