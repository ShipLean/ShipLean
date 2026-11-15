using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Timeline;

// The Feature timeline's usage reports (C5, D14, D20): POST api/projects/{id}/usage/timeline-opened with from = feature | link,
// and POST api/projects/{id}/usage/timeline-bar-selected, for a member of the project. Joel is a member of Ship and not of Part.
[TestFixture]
public class TimelineUsageTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private static string Opened(Guid projectId) => $"api/projects/{projectId}/usage/timeline-opened";

    private static string BarSelected(Guid projectId) => $"api/projects/{projectId}/usage/timeline-bar-selected";

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [TestCase("feature")]
    [TestCase("link")]
    public async Task Opened_ShouldEmitTimelineOpenedWithFrom(string from)
    {
        using var response = await Api.PostJsonAsync(Opened(SeedIds.Projects.Ship), new TimelineOpenedDto(from));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.TimelineOpened);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.ProjectCount.ShouldBe(1);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Properties[UsageProps.From].ShouldBe(from);
    }

    [TestCase("board")]
    [TestCase("Feature")]
    [TestCase("")]
    public async Task Opened_WhenFromIsUnknown_ShouldReturnValidation(string from)
    {
        using var response = await Api.PostJsonAsync(Opened(SeedIds.Projects.Ship), new TimelineOpenedDto(from));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("from");
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task Opened_WhenNotMember_ShouldReturnNotFound()
    {
        using var response = await Api.PostJsonAsync(Opened(SeedIds.Projects.Part), new TimelineOpenedDto("feature"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task BarSelected_ShouldEmit()
    {
        using var response = await Api.PostJsonAsync(BarSelected(SeedIds.Projects.Ship));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.TimelineBarSelected);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Properties.ShouldBeEmpty();
    }

    [Test]
    public async Task BarSelected_WhenNotMember_ShouldReturnNotFound()
    {
        using var response = await Api.PostJsonAsync(BarSelected(SeedIds.Projects.Part));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }
}
