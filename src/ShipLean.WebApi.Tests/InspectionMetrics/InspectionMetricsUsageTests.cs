using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// Inspection's usage reports (SHIP-1398 K11): POST api/projects/{id}/usage/inspection-metrics-viewed {view},
// …-period-opened {period} and …-task-opened {from}, for a member of the project. Joel is a member of Ship and not of Part.
[TestFixture]
public class InspectionMetricsUsageTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private static string Url(Guid projectId, string report) => $"api/projects/{projectId}/usage/inspection-metrics-{report}";

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    // Mutation: drop the view prop from the tracked event.
    [TestCase("overview")]
    [TestCase("leadtime")]
    [TestCase("period")]
    [TestCase("json")]
    public async Task Viewed_WhenPosted_ShouldRecordEventWithView(string view)
    {
        using var response = await Api.PostJsonAsync(Url(SeedIds.Projects.Ship, "viewed"), new InspectionMetricsViewedDto(view));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.InspectionMetricsViewed);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Properties[UsageProps.View].ShouldBe(view);
    }

    [TestCase("finished")]
    [TestCase("running")]
    public async Task PeriodOpened_WhenPosted_ShouldRecordEventWithPeriod(string period)
    {
        using var response = await Api.PostJsonAsync(Url(SeedIds.Projects.Ship, "period-opened"), new InspectionMetricsPeriodOpenedDto(period));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.InspectionMetricsPeriodOpened);
        tracked.Properties[UsageProps.Period].ShouldBe(period);
    }

    [TestCase("rail")]
    [TestCase("longest")]
    [TestCase("chart")]
    public async Task TaskOpened_WhenPosted_ShouldRecordEventWithFrom(string from)
    {
        using var response = await Api.PostJsonAsync(Url(SeedIds.Projects.Ship, "task-opened"), new InspectionMetricsTaskOpenedDto(from));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.InspectionMetricsTaskOpened);
        tracked.Properties[UsageProps.From].ShouldBe(from);
    }

    [TestCase("viewed", "view", "Overview")]
    [TestCase("period-opened", "period", "s23")]
    [TestCase("task-opened", "from", "board")]
    public async Task Report_WhenValueUnknown_ShouldReturnValidation(string report, string field, string value)
    {
        using var response = await Api.PostJsonAsync(Url(SeedIds.Projects.Ship, report), new Dictionary<string, string> { [field] = value });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe(field);
        Recorder.Events.ShouldBeEmpty();
    }

    [Test]
    public async Task Viewed_WhenNotMember_ShouldReturnNotFound()
    {
        using var response = await Api.PostJsonAsync(Url(SeedIds.Projects.Part, "viewed"), new InspectionMetricsViewedDto("overview"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Recorder.Events.ShouldBeEmpty();
    }
}
