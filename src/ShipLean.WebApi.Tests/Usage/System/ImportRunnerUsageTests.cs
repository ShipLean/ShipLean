using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Import.Jobs;
using Microsoft.Extensions.DependencyInjection;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Usage.System;

// The runner tracks how a job ended, once it has written the final state.
[TestFixture]
public class ImportRunnerUsageTests : ImportJobsFixture
{
    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("PLAT-1")];

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [Test]
    public async Task ImportRunner_WhenJobSucceeds_ShouldTrackImportCompleted()
    {
        var started = await StartAsync("UIA");
        var done = await WaitForAsync(started.Id, ImportJobState.Done);

        var tracked = await Recorder.WaitForAsync(e => e.Name == UsageEventNames.ImportCompleted, TimeSpan.FromSeconds(20));

        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
        tracked.Surface.ShouldBe(UsageSurface.System);
        tracked.Actor.ShouldBe(UsageActor.System);
        tracked.ProjectId.ShouldBe(done.ProjectId);
        tracked.Source.ShouldBe("system:ImportRunner");
        tracked.DurationMs.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(0);
        tracked.Properties[UsageProps.Provider].ShouldBe("jira");
        Recorder.Events.ShouldNotContain(e => e.Name == UsageEventNames.ImportFailed);
    }

    [Test]
    public async Task ImportRunner_WhenJobFails_ShouldTrackImportFailed()
    {
        var mapping = await SuggestAsync();
        Jira.Down = true;
        var started = await StartAsync("UIB", mapping);
        await WaitForAsync(started.Id, ImportJobState.Failed);
        Jira.Down = false;

        var tracked = await Recorder.WaitForAsync(e => e.Name == UsageEventNames.ImportFailed, TimeSpan.FromSeconds(20));

        tracked.Outcome.ShouldBe(UsageOutcome.Error);
        tracked.Source.ShouldBe("system:ImportRunner");
        tracked.ProjectId.ShouldBeNull();
        tracked.ProjectCount.ShouldBe(0);
        tracked.Properties.Keys.ShouldBe([UsageProps.Provider]);
        Recorder.Events.ShouldNotContain(e => e.Name == UsageEventNames.ImportCompleted);
    }
}
