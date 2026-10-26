using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Projects.Api;

// The New project form's Sprint 1, with the clock pinned to a Saturday (24 October 2026); one host per class, so every case reads the same day.
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

        sprint.ShouldBe(new SprintCreate("Sprint 1", new DateOnly(2026, 10, 24), new DateOnly(2026, 11, 6), string.Empty));
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
