using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Usage.BacklogSort;

// POST api/projects/{id}/usage/backlog-sort: a known backlog screen, column and direction, for a member of the project.
// Joel is a member of Ship and not of Part.
[TestFixture]
public class BacklogSortTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private static string Url(Guid projectId) => $"api/projects/{projectId}/usage/backlog-sort";

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [Test]
    public async Task BacklogSort_WhenMember_ShouldTrackScreenColumnAndDirection()
    {
        using var response = await Api.PostJsonAsync(Url(SeedIds.Projects.Ship), new BacklogSortDto("featuresBacklog", "progress", "desc"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.BacklogSorted);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.ProjectCount.ShouldBe(1);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Properties[UsageProps.Screen].ShouldBe("featuresBacklog");
        tracked.Properties[UsageProps.Column].ShouldBe("progress");
        tracked.Properties[UsageProps.Direction].ShouldBe("desc");
    }

    [Test]
    public async Task BacklogSort_WhenNotMember_ShouldBeNotFoundAndTrackNothing()
    {
        using var response = await Api.PostJsonAsync(Url(SeedIds.Projects.Part), new BacklogSortDto("storiesBacklog", "points", "asc"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }

    [TestCase("taskboard", "points", "asc", "screen")]
    [TestCase("storiesBacklog", "<script>", "asc", "column")]
    [TestCase("storiesBacklog", "points", "up", "direction")]
    public async Task BacklogSort_WhenValueUnknown_ShouldBeValidationAndTrackNothing(string screen, string column, string direction, string field)
    {
        using var response = await Api.PostJsonAsync(Url(SeedIds.Projects.Ship), new BacklogSortDto(screen, column, direction));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadApiErrorAsync();
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe(field);
        Recorder.Events.ShouldBeEmpty();
    }
}
