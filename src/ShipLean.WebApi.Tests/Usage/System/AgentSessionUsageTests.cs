using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
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

    // C12: each terminal session is counted once with the end_reason its writer recorded, so the share of each reason (and of
    // unknown) over a release is the measure.
    [TestCase("response", AgentEndReasons.Completed)]
    [TestCase("error", AgentEndReasons.AgentError)]
    public async Task AgentSession_WhenItEnds_ShouldTrackFinishedOnceWithItsEndReason(string type, string endReason)
    {
        var (arranged, sessionId) = await OpenSessionAsync();

        await ReportAsync(arranged, sessionId, type);

        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.AgentSessionFinished).ShouldHaveSingleItem();
        tracked.Properties[UsageProps.EndReason].ShouldBe(endReason);
    }

    // C12: the two reasons that reach the notifier through FailedAsync's transaction (a created out of retries) and FailAsync's overwrite.
    [Test]
    public async Task AgentSession_WhenItsDeliveryRunsOutOfTries_ShouldTrackFinishedWithDeliveryFailed()
    {
        var (_, sessionId) = await OpenSessionAsync();
        var start = TimeProvider.System.GetUtcNow();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE agent_delivery SET status = 'failed', last_error = 'Set aside by a test.' WHERE status = 'pending' AND session_id <> @sessionId", new { sessionId });
            await database.ExecuteAsync("UPDATE agent_delivery SET next_attempt_at = @start WHERE session_id = @sessionId", new { sessionId, start });
        }

        var clock = new FakeTimeProvider(start);
        using var receiver = new StubReceiver().Then(HttpStatusCode.BadGateway, HttpStatusCode.BadGateway);
        using var relay = receiver.Client();
        var deliverer = new AgentDeliverer(
            Host.Services.GetRequiredService<NpgsqlDataSource>(),
            Host.Services.GetRequiredService<IAgentConnections>(),
            new StubHttpClientFactory(relay),
            Host.Services.GetRequiredService<AgentSessionNotifier>(),
            Host.Services.GetRequiredService<IAgentDeliveryAttempts>(),
            clock,
            NullLogger<AgentDeliverer>.Instance);
        await deliverer.RunDueAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        await deliverer.RunDueAsync(CancellationToken.None);

        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.AgentSessionFinished).ShouldHaveSingleItem();
        tracked.Properties[UsageProps.EndReason].ShouldBe(AgentEndReasons.DeliveryFailed);
    }

    [Test]
    public async Task AgentSession_WhenAProposeSessionsItemHasNoGitHubIssue_ShouldTrackFinishedWithProposalWithoutIssue()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = (await arranged.CreateItemAsync(Api)).Id;
        var issues = Host.Services.GetRequiredService<IGitHubIssueStore>();
        var issue = await issues.InsertAsync(
            new IssueRef(IssueRef.GitHubHost, "shiplean-tests/usage-end-reasons", Random.Shared.Next(1, int.MaxValue)), arranged.Project.Id, item, "reporter",
            "Login button says Sing in", "The login page says Sing in.", IssuePhase.Proposing);
        var sessionId = (await Host.Services.GetRequiredService<AgentSessionStarter>().StartAsync(
            item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None, AgentSessionMode.Propose)).SessionId!.Value;
        await issues.RepointAsync(issue.Id, (await arranged.CreateItemAsync(Api)).Id);
        Recorder.Clear();

        await ReportAsync(arranged, sessionId, "response");

        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.AgentSessionFinished).ShouldHaveSingleItem();
        tracked.Properties[UsageProps.EndReason].ShouldBe(AgentEndReasons.ProposalWithoutIssue);
    }

    [Test]
    public async Task AgentSession_WhenStoppedAndTheGraceExpires_ShouldTrackFinishedWithCanceled()
    {
        var (_, sessionId) = await OpenSessionAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE agent_session SET stop_requested_at = @at WHERE id = @sessionId", new { sessionId, at = TimeProvider.System.GetUtcNow().AddHours(-1) });
        }

        await Host.Services.GetRequiredService<AgentDeliverer>().CancelUnansweredStopsAsync(CancellationToken.None);

        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.AgentSessionFinished).ShouldHaveSingleItem();
        (tracked.Outcome, tracked.Properties[UsageProps.EndReason]).ShouldBe((UsageOutcome.Cancelled, AgentEndReasons.Canceled));
    }

    // D8: stale is not terminal (a later activity revives it), so the sweep tracks nothing.
    [Test]
    public async Task AgentSession_WhenSweptStale_ShouldNotTrackFinished()
    {
        var (_, sessionId) = await OpenSessionAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE agent_session SET last_activity_at = @at WHERE id = @sessionId", new { sessionId, at = TimeProvider.System.GetUtcNow().AddHours(-2) });
        }

        (await Host.Services.GetRequiredService<AgentStaleSweeper>().SweepAsync(CancellationToken.None)).ShouldBeGreaterThanOrEqualTo(1);

        Recorder.Events.ShouldNotContain(e => e.Name == UsageEventNames.AgentSessionFinished);
    }

    [Test]
    public async Task AgentSession_WhenStillWorking_ShouldNotTrackFinished()
    {
        var (arranged, sessionId) = await OpenSessionAsync();

        await ReportAsync(arranged, sessionId, "thought");

        Recorder.Events.ShouldNotContain(e => e.Name == UsageEventNames.AgentSessionFinished);
    }
}
