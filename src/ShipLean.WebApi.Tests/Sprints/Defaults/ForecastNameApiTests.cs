using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Sprints.Defaults;

// The forecast names the sprints it adds after the last sprint's number; a name ending in a number too large to count falls
// back to the sprint count. The clock is pinned after every sprint so the whole backlog lands in sprints the forecast names.
[TestFixture]
public class ForecastNameApiTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero)));

    [Test]
    public async Task Forecast_WhenALastSprintNameEndsInANumberTooLargeToCount_ShouldNameTheNextOnesByCount()
    {
        using var created = await Api.PostJsonAsync(
            $"api/projects/{SeedIds.Projects.Ship}/sprints",
            new SprintCreate("Build 20261024123456", new DateOnly(2030, 1, 7), new DateOnly(2030, 1, 18), string.Empty));
        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        using var set = await Api.PutJsonAsync($"api/projects/{SeedIds.Projects.Ship}/forecast", new VelocityOverridePut(30m));
        set.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var sprints = await Api.GetAsync<List<SprintDto>>($"api/projects/{SeedIds.Projects.Ship}/sprints");

        var forecast = await Api.GetAsync<ForecastDto>($"api/projects/{SeedIds.Projects.Ship}/forecast");

        forecast.Sprints.First(s => s.SprintId is null).Name.ShouldBe($"Sprint {sprints.Count + 1}");
    }
}
