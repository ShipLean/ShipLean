using ShipLean.WebApi.Tests.Taskboard;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// POST api/projects/{id}/usage/inspection: each action tracks its own event for the project.
[TestFixture]
public class InspectionUsageTests : InspectionApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [OneTimeSetUp]
    public async Task EnableProject()
    {
        using var response = await TurnOnAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [TestCase(InspectionUsageAction.SectionSeen, UsageEventNames.InspectionSectionSeen)]
    [TestCase(InspectionUsageAction.RunOpened, UsageEventNames.InspectionRunOpened)]
    [TestCase(InspectionUsageAction.RowSelected, UsageEventNames.InspectionRowSelected)]
    [TestCase(InspectionUsageAction.Zoomed, UsageEventNames.InspectionZoomed)]
    [TestCase(InspectionUsageAction.ErrorsOnly, UsageEventNames.InspectionErrorsOnly)]
    public async Task Report_WhenMember_ShouldTrackTheActionsEvent(InspectionUsageAction action, string eventName)
    {
        using var response = await Api.PostWireAsync($"api/projects/{SeedIds.Projects.Ship}/usage/inspection", new InspectionUsageDto(action, Item));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var tracked = Recorder.Events.ShouldHaveSingleItem();
        tracked.Name.ShouldBe(eventName);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }
}
