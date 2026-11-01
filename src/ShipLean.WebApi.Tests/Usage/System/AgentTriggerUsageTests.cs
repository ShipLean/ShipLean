using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Usage;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.Usage.System;

// The host's own AgentTrigger subscriber runs; the test waits on the usage event the trigger tracks after it started a session.
[TestFixture]
public class AgentTriggerUsageTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [Test]
    public async Task AgentTrigger_WhenTransitionFires_ShouldTrackSessionOpenedWithProject()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        await AgentDispatchArrange.AddTransitionAsync(database, arranged.Project.Id, WorkItemTypeId.Story, arranged.Project.State(WorkItemTypeId.Story, "New"), arranged.Active);
        var item = await arranged.CreateItemAsync(Api);

        await AgentDispatchArrange.MoveAsync(Api, item.Id, item.Revision, arranged.Active);

        var tracked = await Recorder.WaitForAsync(e => e.Name == UsageEventNames.AgentSessionOpened, TimeSpan.FromSeconds(20));
        tracked.Surface.ShouldBe(UsageSurface.System);
        tracked.Actor.ShouldBe(UsageActor.System);
        tracked.ProjectId.ShouldBe(arranged.Project.Id);
        tracked.ProjectCount.ShouldBe(1);
        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
        tracked.Source.ShouldBe("system:AgentTrigger");
    }
}
