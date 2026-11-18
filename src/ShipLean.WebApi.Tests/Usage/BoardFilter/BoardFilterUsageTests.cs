using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Usage.BoardFilter;

// The board filter's usage reports: POST api/projects/{id}/usage/board-filter-opened with the board level, and board-health-filtered with the chosen
// health, for a member of the project. Joel is a member of Ship and not of Part.
[TestFixture]
public class BoardFilterUsageTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private static string Opened(Guid projectId) => $"api/projects/{projectId}/usage/board-filter-opened";

    private static string Health(Guid projectId) => $"api/projects/{projectId}/usage/board-health-filtered";

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [TestCase("features")]
    [TestCase("stories")]
    [TestCase("epics")]
    public async Task FilterOpened_WhenMember_ShouldEmitBoardFilterOpenedWithTheLevel(string level)
    {
        using var response = await Api.PostJsonAsync(Opened(SeedIds.Projects.Ship), new BoardFilterOpenedDto(level));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.BoardFilterOpened);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Properties[UsageProps.Level].ShouldBe(level);
    }

    [TestCase("backlog")]
    [TestCase("Features")]
    [TestCase("1")]
    [TestCase("")]
    public async Task FilterOpened_WhenLevelIsUnknown_ShouldReturnValidationAndTrackNothing(string level)
    {
        using var response = await Api.PostJsonAsync(Opened(SeedIds.Projects.Ship), new BoardFilterOpenedDto(level));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("level");
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task FilterOpened_WhenNotMember_ShouldReturnNotFoundAndTrackNothing()
    {
        using var response = await Api.PostJsonAsync(Opened(SeedIds.Projects.Part), new BoardFilterOpenedDto("features"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }

    [TestCase("onTrack")]
    [TestCase("atRisk")]
    [TestCase("blocked")]
    [TestCase("none")]
    [TestCase("any")]
    public async Task HealthFiltered_WhenMember_ShouldEmitTheChosenHealth(string health)
    {
        using var response = await Api.PostJsonAsync(Health(SeedIds.Projects.Ship), new BoardHealthFilteredDto(health));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.BoardHealthFiltered);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.ProjectCount.ShouldBe(1);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Properties[UsageProps.Health].ShouldBe(health);
    }

    [TestCase("great")]
    [TestCase("1")]
    [TestCase("OnTrack")]
    [TestCase("")]
    public async Task HealthFiltered_WhenHealthIsUnknown_ShouldReturnValidationAndTrackNothing(string health)
    {
        using var response = await Api.PostJsonAsync(Health(SeedIds.Projects.Ship), new BoardHealthFilteredDto(health));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("health");
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task HealthFiltered_WhenNotMember_ShouldReturnNotFoundAndTrackNothing()
    {
        using var response = await Api.PostJsonAsync(Health(SeedIds.Projects.Part), new BoardHealthFilteredDto("blocked"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }
}
