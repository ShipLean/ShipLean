using System.Globalization;
using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.AgentDispatch.EndReasons;

// C2: the writers that set a session's state from the agent's reports and the stale sweep also set end_reason and end_detail in the
// same UPDATE. Sessions are opened through Start agent; reports go through IAgentReports as the session's connection.
[TestFixture]
public class AgentEndReasonWriterTests : ApiFixture
{
    private async Task<(AgentDispatchArrange Arranged, Guid ItemId, Guid SessionId)> OpenSessionAsync(NpgsqlConnection database)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var started = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        started.StatusCode.ShouldBe(HttpStatusCode.OK, await started.Content.ReadAsStringAsync());
        return (arranged, item.Id, (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem().Id);
    }

    private async Task ReportAsync(AgentDispatchArrange arranged, Guid sessionId, string type, string body)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAgentReports>().CreateActivityAsync(
            new AgentReporter(arranged.ConnectionId!.Value, "Relay"),
            new AgentActivityCreate(sessionId, type, body, null, null, null, false, null, null));
    }

    private static Task<(string State, string? Reason, string? Detail)> SessionAsync(NpgsqlConnection database, Guid sessionId)
        => database.QuerySingleAsync<(string State, string? Reason, string? Detail)>(
            "SELECT state, end_reason, end_detail FROM agent_session WHERE id = @sessionId", new { sessionId });

    [Test]
    public async Task Error_WhenTheAgentReportsOne_ShouldEndAsAgentErrorWithTheFirstThousandCharacters()
    {
        await using var database = await OpenDatabaseAsync();
        var (arranged, _, sessionId) = await OpenSessionAsync(database);
        var body = "dotnet build failed: CS0103\n" + string.Concat(Enumerable.Range(0, 200).Select(i => $"line {i:D3} "));

        await ReportAsync(arranged, sessionId, "error", body);

        body.Length.ShouldBeGreaterThan(1000);
        (await SessionAsync(database, sessionId)).ShouldBe(("failed", AgentEndReasons.AgentError, body[..1000]));
    }

    [Test]
    public async Task Error_WhenTheBodyIsShort_ShouldKeepItWhole()
    {
        await using var database = await OpenDatabaseAsync();
        var (arranged, _, sessionId) = await OpenSessionAsync(database);

        await ReportAsync(arranged, sessionId, "error", "The tests failed.");

        (await SessionAsync(database, sessionId)).ShouldBe(("failed", AgentEndReasons.AgentError, "The tests failed."));
    }

    [Test]
    public async Task Sweep_WhenSilentForThirtyMinutes_ShouldRecordStaleAndARevivingActivityShouldClearIt()
    {
        await using var database = await OpenDatabaseAsync();
        var (arranged, _, sessionId) = await OpenSessionAsync(database);
        var lastActivity = DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds() - 7200);
        await database.ExecuteAsync("UPDATE agent_session SET last_activity_at = @lastActivity WHERE id = @sessionId", new { sessionId, lastActivity });

        await Host.Services.GetRequiredService<AgentStaleSweeper>().SweepAsync(CancellationToken.None);

        var since = lastActivity.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        (await SessionAsync(database, sessionId)).ShouldBe(("stale", AgentEndReasons.Stale, $"No activity for 30 minutes since {since}."));

        await ReportAsync(arranged, sessionId, "thought", "Back again.");

        (await SessionAsync(database, sessionId)).ShouldBe(("working", null, null));
    }

    [Test]
    public async Task Response_WhenAProposeSessionsItemHasNoGitHubIssue_ShouldEndAsProposalWithoutIssue()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = (await arranged.CreateItemAsync(Api)).Id;
        var issues = Host.Services.GetRequiredService<IGitHubIssueStore>();
        var issue = await issues.InsertAsync(
            new IssueRef(IssueRef.GitHubHost, "shiplean-tests/end-reasons", Random.Shared.Next(1, int.MaxValue)), arranged.Project.Id, item, "reporter",
            "Login button says Sing in", "The login page says Sing in.", IssuePhase.Proposing);
        var sessionId = (await Host.Services.GetRequiredService<AgentSessionStarter>().StartAsync(
            item, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None, AgentSessionMode.Propose)).SessionId!.Value;
        await issues.RepointAsync(issue.Id, (await arranged.CreateItemAsync(Api)).Id);

        await ReportAsync(arranged, sessionId, "response", "A proposal for an item that lost its issue.");

        (await SessionAsync(database, sessionId)).ShouldBe(("failed", AgentEndReasons.ProposalWithoutIssue, "The item has no GitHub issue to propose on."));
    }
}
