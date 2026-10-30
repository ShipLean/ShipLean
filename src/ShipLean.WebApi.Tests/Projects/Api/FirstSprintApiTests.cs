using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Projects.Api;

// The New project form's first sprint, named for the year and quarter, with the clock pinned to a Saturday (24 October 2026); one host per class, so every case reads the same day.
[TestFixture]
public class FirstSprintApiTests : ApiFixture
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(_clock);

    [Test]
    public async Task FirstSprint_WhenTodayIsSaturdayAndNotAWorkingDay_ShouldStartOnMondayAndKeepTwoWeeks()
    {
        var sprint = await Api.GetAsync<SprintCreate>("api/projects/first-sprint?workingDays=1,2,3,4,5");

        sprint.StartDate.ShouldBe(new DateOnly(2026, 10, 26));
        sprint.EndDate.ShouldBe(new DateOnly(2026, 11, 8));
    }

    [Test]
    public async Task FirstSprint_WhenSaturdayIsAWorkingDay_ShouldStartToday()
    {
        var sprint = await Api.GetAsync<SprintCreate>("api/projects/first-sprint?workingDays=6,7");

        sprint.ShouldBe(new SprintCreate("2026 Q4 Sprint 2", new DateOnly(2026, 10, 24), new DateOnly(2026, 11, 6), string.Empty));
    }

    [Test]
    public async Task FirstSprint_WhenStartIsGiven_ShouldNameTheSprintAfterItsQuarter()
    {
        var q1 = await Api.GetAsync<SprintCreate>("api/projects/first-sprint?workingDays=1,2,3,4,5&start=2027-01-04");
        var q2 = await Api.GetAsync<SprintCreate>("api/projects/first-sprint?workingDays=1,2,3,4,5&start=2027-04-19&end=2027-04-25");

        q1.ShouldBe(new SprintCreate("2027 Q1 Sprint 1", new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 17), string.Empty));
        q2.ShouldBe(new SprintCreate("2027 Q2 Sprint 3", new DateOnly(2027, 4, 19), new DateOnly(2027, 4, 25), string.Empty));
    }

    [Test]
    public async Task FirstSprint_WhenEndIsBeforeStart_ShouldBeRefused()
    {
        using var response = await Api.GetAsync("api/projects/first-sprint?workingDays=1&start=2027-01-10&end=2027-01-09");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task FirstSprint_WhenOnlySundayWorks_ShouldStartTomorrow()
    {
        var sprint = await Api.GetAsync<SprintCreate>("api/projects/first-sprint?workingDays=7");

        sprint.StartDate.ShouldBe(new DateOnly(2026, 10, 25));
    }

    [TestCase("")]
    [TestCase("0")]
    [TestCase("8")]
    [TestCase("1,x")]
    public async Task FirstSprint_WhenWorkingDaysAreNotDays_ShouldBeRefused(string days)
    {
        using var response = await Api.GetAsync($"api/projects/first-sprint?workingDays={days}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task FirstSprint_WhenSignedOut_ShouldBeUnauthorized()
    {
        using var response = await Api.ActingAs(TestUser.Anonymous).GetAsync("api/projects/first-sprint?workingDays=1");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
