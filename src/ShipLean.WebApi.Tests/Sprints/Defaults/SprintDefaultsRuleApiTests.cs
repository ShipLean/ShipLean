using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Sprints.Defaults;

// The defaults rules on sprints these tests arrange themselves, one project each, with the clock pinned on a Saturday
// (24 October 2026): the length is the last sprint's in calendar days, only the start moves to a working day, and the
// name follows the highest number. Pay is read with no sprints first; its name tests run after, in order.
[TestFixture]
public class SprintDefaultsRuleApiTests : ApiFixture
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<TimeProvider>(_clock);

    [Test]
    [Order(1)]
    public async Task Next_WhenNoSprintsOnASaturday_ShouldStartOnTheMonday()
    {
        var next = await NextAsync(SeedIds.Projects.Pay);

        next.Name.ShouldBe("Sprint 1");
        next.StartDate.ShouldBe(new DateOnly(2026, 10, 26));
        next.EndDate.ShouldBe(new DateOnly(2026, 11, 8));
    }

    [Test]
    public async Task Next_WhenLastSprintWasThreeWeeks_ShouldBeThreeWeeks()
    {
        await CreateAsync(SeedIds.Projects.Mob, "Sprint 1", new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 24));

        var next = await NextAsync(SeedIds.Projects.Mob);

        next.StartDate.ShouldBe(new DateOnly(2026, 11, 25));
        next.EndDate.ShouldBe(new DateOnly(2026, 12, 15));
    }

    [Test]
    public async Task Next_WhenLastSprintEndsOnAFriday_ShouldStartOnTheMonday()
    {
        await CreateAsync(SeedIds.Projects.Data, "Sprint 1", new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 13));

        var next = await NextAsync(SeedIds.Projects.Data);

        next.StartDate.ShouldBe(new DateOnly(2026, 11, 16));
        next.EndDate.ShouldBe(new DateOnly(2026, 11, 27));
    }

    [Test]
    public async Task Next_WhenLastSprintEndsOnASunday_ShouldKeepItsLengthWithoutClipping()
    {
        await CreateAsync(SeedIds.Projects.Ops, "Sprint 1", new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 22));

        var next = await NextAsync(SeedIds.Projects.Ops);

        next.StartDate.ShouldBe(new DateOnly(2026, 11, 23));
        next.EndDate.ShouldBe(new DateOnly(2026, 12, 13));
    }

    [Test]
    [Order(2)]
    public async Task Next_WhenAnEarlierSprintHasTheHigherNumber_ShouldUseTheHighest()
    {
        await CreateAsync(SeedIds.Projects.Pay, "Sprint 4", new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 15));
        await CreateAsync(SeedIds.Projects.Pay, "Sprint 3", new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 12));

        var next = await NextAsync(SeedIds.Projects.Pay);

        next.Name.ShouldBe("Sprint 5");
        using var created = await Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Pay}/sprints", next);
        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
    }

    [Test]
    [Order(3)]
    public async Task Next_WhenANameEndsInANumberTooLargeToCount_ShouldFallBackToTheCount()
    {
        await CreateAsync(SeedIds.Projects.Pay, "Build 20261024123456", new DateOnly(2027, 6, 1), new DateOnly(2027, 6, 11));

        var next = await NextAsync(SeedIds.Projects.Pay);

        next.Name.ShouldBe("Sprint 6");
    }

    private async Task<SprintCreate> NextAsync(Guid projectId) =>
        await Api.GetAsync<SprintCreate>($"api/projects/{projectId}/sprints/next");

    private async Task CreateAsync(Guid projectId, string name, DateOnly start, DateOnly end)
    {
        using var response = await Api.PostJsonAsync($"api/projects/{projectId}/sprints", new SprintCreate(name, start, end, string.Empty));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
