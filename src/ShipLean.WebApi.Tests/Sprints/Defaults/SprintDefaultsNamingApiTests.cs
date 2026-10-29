using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Sprints.Defaults;

// The New sprint form's name under each naming convention, with the clock pinned on a Saturday (24 October 2026). One project
// per test (Pay, Mob, Data, Ops have no sprints); a seeded project keeps Number until its setting is changed.
[TestFixture]
public class SprintDefaultsNamingApiTests : ApiFixture
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<TimeProvider>(_clock);

    [Test]
    public async Task Next_WhenProjectUsesYearQuarterNumber_ShouldNameTheQuarterAndPlace()
    {
        await UseAsync(SeedIds.Projects.Pay, "yearQuarterNumber");

        var next = await NextAsync(SeedIds.Projects.Pay);

        // 26 October is the 25th day of the quarter: 1 + 25 / 14.
        next.ShouldBe(new SprintCreate("2026 Q4 Sprint 2", new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 8), string.Empty));
    }

    [Test]
    public async Task Next_WhenProjectUsesNumber_ShouldKeepSprintNumber()
    {
        await UseAsync(SeedIds.Projects.Mob, "number");

        (await NextAsync(SeedIds.Projects.Mob)).Name.ShouldBe("Sprint 1");
    }

    [Test]
    public async Task Next_WhenDatesAreTyped_ShouldNameAndLengthFollowThem()
    {
        await UseAsync(SeedIds.Projects.Data, "yearQuarterNumber");

        var next = await Api.GetAsync<SprintCreate>($"api/projects/{SeedIds.Projects.Data}/sprints/next?start=2027-01-04&end=2027-01-10");
        var crossing = await Api.GetAsync<SprintCreate>($"api/projects/{SeedIds.Projects.Data}/sprints/next?start=2027-03-29");

        next.ShouldBe(new SprintCreate("2027 Q1 Sprint 1", new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 10), string.Empty));
        crossing.Name.ShouldBe("2027 Q1 Sprint 7");
        crossing.EndDate.ShouldBe(new DateOnly(2027, 4, 11));
    }

    [Test]
    public async Task Next_WhenTheComputedNameIsTaken_ShouldTakeTheNextNumber()
    {
        await UseAsync(SeedIds.Projects.Ops, "yearQuarterNumber");
        await CreateAsync(SeedIds.Projects.Ops, "2026 Q4 Sprint 2", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 25));

        var next = await NextAsync(SeedIds.Projects.Ops);

        next.Name.ShouldBe("2026 Q4 Sprint 3");
    }

    [Test]
    public async Task Next_WhenEndIsBeforeStart_ShouldBeRefused()
    {
        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Pay}/sprints/next?start=2027-01-10&end=2027-01-09");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    private async Task UseAsync(Guid projectId, string naming)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE project SET sprint_naming = @naming WHERE id = @projectId", new { naming, projectId });
    }

    private async Task<SprintCreate> NextAsync(Guid projectId) =>
        await Api.GetAsync<SprintCreate>($"api/projects/{projectId}/sprints/next");

    private async Task CreateAsync(Guid projectId, string name, DateOnly start, DateOnly end)
    {
        using var response = await Api.PostJsonAsync($"api/projects/{projectId}/sprints", new SprintCreate(name, start, end, string.Empty));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
