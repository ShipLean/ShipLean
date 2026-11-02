using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.Usage.System;

// A session reaching a terminal state through the agent's report is tracked once by the notifier.
[TestFixture]
public class AgentSessionUsageTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    private async Task<(AgentDispatchArrange Arranged, Guid SessionId)> OpenSessionAsync()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var started = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        started.StatusCode.ShouldBe(HttpStatusCode.OK, await started.Content.ReadAsStringAsync());
        return (arranged, (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem().Id);
    }

    private async Task ReportAsync(AgentDispatchArrange arranged, Guid sessionId, string type)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IAgentReports>();
        await reports.CreateActivityAsync(
            new AgentReporter(arranged.ConnectionId!.Value, "Relay"),
            new AgentActivityCreate(sessionId, type, "done", null, null, null, false, null, null));
    }

    [Test]
    public async Task AgentSession_WhenFinished_ShouldTrackFinishedOk()
    {
        var (arranged, sessionId) = await OpenSessionAsync();

        await ReportAsync(arranged, sessionId, "response");

        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.AgentSessionFinished).ShouldHaveSingleItem();
        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
        tracked.Surface.ShouldBe(UsageSurface.System);
        tracked.Actor.ShouldBe(UsageActor.System);
        tracked.ProjectId.ShouldBe(arranged.Project.Id);
        tracked.Source.ShouldBe("system:AgentSessionNotifier");
    }

    [Test]
    public async Task AgentSession_WhenFailed_ShouldTrackFinishedError()
    {
        var (arranged, sessionId) = await OpenSessionAsync();

        await ReportAsync(arranged, sessionId, "error");

        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.AgentSessionFinished).ShouldHaveSingleItem();
        tracked.Outcome.ShouldBe(UsageOutcome.Error);
        tracked.ProjectId.ShouldBe(arranged.Project.Id);
    }

    [Test]
    public async Task AgentSession_WhenStillWorking_ShouldNotTrackFinished()
    {
        var (arranged, sessionId) = await OpenSessionAsync();

        await ReportAsync(arranged, sessionId, "thought");

        Recorder.Events.ShouldNotContain(e => e.Name == UsageEventNames.AgentSessionFinished);
    }
}
