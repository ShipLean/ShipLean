using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.IssueIntake;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.IssueIntake.Rounds;

// Proposal rounds (C4, D10, D11, D16, D18): RequestRoundAsync's rate limit, round cap, hold and one-round-per-request rules in the
// caller's transaction, and the round job worker's idempotent propose session start with the round prompt. Each test arranges its
// own project, so the per-project rate count starts at zero; the worker claims any due round job, so each test ends by completing
// whatever it left pending.
[TestFixture]
public class RoundTests : ApiFixture
{
    private const string Instructions = "Project proposal instructions sentinel.";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<FixedIssueIntakeSettings>();
        services.AddScoped<IIssueIntakeSettings>(sp => sp.GetRequiredService<FixedIssueIntakeSettings>());
    };

    private IIssueProposalSessions Sessions => Host.Services.GetRequiredService<IIssueProposalSessions>();

    private RoundJobWorker Worker => Host.Services.GetRequiredService<RoundJobWorker>();

    [TearDown]
    public async Task CompleteLeftoversAsync()
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE status = 'pending'");
    }

    [Test]
    public async Task RequestRound_WhenRateExceeded_ShouldHoldOnce()
    {
        var (arranged, _, issue) = await ArrangeAsync();
        await FillRateAsync(arranged, issue);

        var first = await RequestAsync(issue.Id, force: false);
        var second = await RequestAsync(issue.Id, force: false);

        first.ShouldBe(RoundRequest.Held(RoundHolds.RateLimit));
        second.ShouldBe(RoundRequest.Held(RoundHolds.RateLimit));
        var stored = await IssueAsync(issue.Id);
        stored.Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        stored.Rounds.ShouldBe(0);
        (await PendingRoundsAsync(issue.Id)).ShouldBeEmpty();
        var held = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        held.DedupeKey.ShouldBe(RoundHolds.DedupeKey(issue.Id, RoundHolds.RateLimit));
        held.Body.ShouldBe(RoundHolds.Comment);
    }

    [Test]
    public async Task RequestRound_WhenCapReached_ShouldHold()
    {
        var (_, _, issue) = await ArrangeAsync(maxRounds: 1);
        await SetRoundsAsync(issue.Id, 1);

        var outcome = await RequestAsync(issue.Id, force: false);

        outcome.ShouldBe(RoundRequest.Held(RoundHolds.RoundCap));
        (await IssueAsync(issue.Id)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        (await PendingRoundsAsync(issue.Id)).ShouldBeEmpty();
        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().DedupeKey.ShouldBe(RoundHolds.DedupeKey(issue.Id, RoundHolds.RoundCap));
    }

    [Test]
    public async Task RequestRound_WhenForced_ShouldIgnoreRateAndCap()
    {
        var (arranged, _, issue) = await ArrangeAsync(maxRounds: 1);
        await SetRoundsAsync(issue.Id, 1);
        await FillRateAsync(arranged, issue);
        await SetPhaseAsync(issue.Id, IssuePhase.WaitingForMaintainer);

        var outcome = await RequestAsync(issue.Id, force: true);

        outcome.ShouldBe(RoundRequest.Requested);
        var stored = await IssueAsync(issue.Id);
        stored.Rounds.ShouldBe(2);
        stored.Phase.ShouldBe(IssuePhase.Proposing);
        var job = (await PendingRoundsAsync(issue.Id)).ShouldHaveSingleItem();
        job.Key.ShouldBe(IssueProposalSessions.JobKey(2, issue.Id));
        job.Payload.ShouldBe("""{"round": 2}""");
        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task RequestRound_WhenConcurrent_ShouldEnqueueOne()
    {
        var (_, _, issue) = await ArrangeAsync();
        await using var first = await OpenDatabaseAsync();
        await using var firstTx = await first.BeginTransactionAsync();
        await using var second = await OpenDatabaseAsync();
        await using var secondTx = await second.BeginTransactionAsync();

        (await Sessions.RequestRoundAsync(issue.Id, false, firstTx)).ShouldBe(RoundRequest.Requested);
        var losing = Sessions.RequestRoundAsync(issue.Id, false, secondTx);
        await WaitUntilBlockedAsync(second.ProcessID, losing);
        await firstTx.CommitAsync();
        (await losing).ShouldBe(RoundRequest.Requested);
        await secondTx.CommitAsync();

        (await IssueAsync(issue.Id)).Rounds.ShouldBe(1);
        (await PendingRoundsAsync(issue.Id)).ShouldHaveSingleItem().Key.ShouldBe(IssueProposalSessions.JobKey(1, issue.Id));
    }

    [Test]
    public async Task RequestRound_WhenTransactionRollsBack_ShouldLeaveNothing()
    {
        var (_, _, issue) = await ArrangeAsync();
        var (heldProject, _, held) = await ArrangeAsync();
        await SetPhaseAsync(issue.Id, IssuePhase.AwaitingReply);
        await FillRateAsync(heldProject, held);

        await using (var database = await OpenDatabaseAsync())
        {
            await using var transaction = await database.BeginTransactionAsync();
            (await Sessions.RequestRoundAsync(issue.Id, false, transaction)).ShouldBe(RoundRequest.Requested);
            (await Sessions.RequestRoundAsync(held.Id, false, transaction)).ShouldBe(RoundRequest.Held(RoundHolds.RateLimit));
            await transaction.RollbackAsync();
        }

        var stored = await IssueAsync(issue.Id);
        stored.Rounds.ShouldBe(0);
        stored.Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await PendingRoundsAsync(issue.Id)).ShouldBeEmpty();
        (await IssueAsync(held.Id)).Phase.ShouldBe(IssuePhase.Proposing);
        (await OutboxAsync(held.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Worker_ShouldStartProposeSessionWithInstructionsAndMatches()
    {
        var (arranged, item, issue) = await ArrangeAsync(title: "Export the sprint burndown chart");
        var match = await AddMatchAsync(arranged, "Export the sprint burndown chart as an image");
        (await RequestAsync(issue.Id, force: false)).ShouldBe(RoundRequest.Requested);

        (await Worker.RunNextAsync(CancellationToken.None)).ShouldBeTrue();

        await using var database = await OpenDatabaseAsync();
        var session = (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldHaveSingleItem();
        session.CreatedBy.ShouldBe(SeedIds.Users.Joel);
        (await database.ExecuteScalarAsync<string>("SELECT mode FROM agent_session WHERE id = @Id", new { session.Id })).ShouldBe("propose");
        var body = (await AgentDispatchArrange.DeliveriesAsync(database, item)).ShouldHaveSingleItem().Body;
        body.ShouldContain(Instructions);
        body.ShouldContain("This is round 1 of the proposal");
        body.ShouldContain("Closest existing items");
        body.ShouldContain(match.Key);
        body.ShouldContain($"https://github.com/shiplean-tests/rounds/pull/{match.Pull}");
        body.ShouldContain(match.Sha);
        (await JobStatusesAsync(issue.Id)).ShouldBe(["done"]);
    }

    [Test]
    public async Task Worker_WhenCrashAfterStart_ShouldNotStartTwice()
    {
        var (_, item, issue) = await ArrangeAsync();
        (await RequestAsync(issue.Id, force: false)).ShouldBe(RoundRequest.Requested);
        (await Worker.RunNextAsync(CancellationToken.None)).ShouldBeTrue();
        await using var database = await OpenDatabaseAsync();
        // The crash: the session started and ended, but the job's completion never committed, so its lease ran out.
        await database.ExecuteAsync("UPDATE agent_session SET state = 'completed', ended_at = now() WHERE work_item_id = @item", new { item });
        await database.ExecuteAsync(
            "UPDATE issue_job SET status = 'pending', done_at = NULL, leased_until = NULL, lease_token = NULL WHERE github_issue_id = @Id", new { issue.Id });

        (await Worker.RunNextAsync(CancellationToken.None)).ShouldBeTrue();

        (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldHaveSingleItem();
        (await JobStatusesAsync(issue.Id)).ShouldBe(["done"]);
        (await IssueAsync(issue.Id)).Phase.ShouldBe(IssuePhase.Proposing);
        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Worker_WhenNoConnection_ShouldHold()
    {
        var (_, item, issue) = await ArrangeAsync(connection: false);
        (await RequestAsync(issue.Id, force: false)).ShouldBe(RoundRequest.Requested);

        (await Worker.RunNextAsync(CancellationToken.None)).ShouldBeTrue();

        await using var database = await OpenDatabaseAsync();
        (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldBeEmpty();
        (await JobStatusesAsync(issue.Id)).ShouldBe(["done"]);
        (await IssueAsync(issue.Id)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        var held = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        held.DedupeKey.ShouldBe(RoundHolds.DedupeKey(issue.Id, "noConnection"));
        held.Body.ShouldBe(RoundHolds.Comment);
    }

    [Test]
    public async Task Prompt_ShouldNotContainDescriptionsOrPeople()
    {
        var (arranged, item, issue) = await ArrangeAsync(title: "Archive old retrospectives automatically");
        var match = await AddMatchAsync(arranged, "Archive old retrospectives");
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            "UPDATE work_item SET description = 'private-match-description-sentinel', assignee_id = @joel WHERE id = @Id", new { match.Id, joel = SeedIds.Users.Joel });
        using (var posted = await Api.PostJsonAsync($"api/work-items/{match.Id}/comments", new { body = "private-match-comment-sentinel" }))
        {
            posted.EnsureSuccessStatusCode();
        }
        await IndexAsync();
        var name = await database.QuerySingleAsync<string>("SELECT name FROM app_user WHERE id = @joel", new { joel = SeedIds.Users.Joel });
        var email = await database.QuerySingleAsync<string>("SELECT email FROM app_user WHERE id = @joel", new { joel = SeedIds.Users.Joel });
        (await RequestAsync(issue.Id, force: false)).ShouldBe(RoundRequest.Requested);

        (await Worker.RunNextAsync(CancellationToken.None)).ShouldBeTrue();

        var body = (await AgentDispatchArrange.DeliveriesAsync(database, item)).ShouldHaveSingleItem().Body;
        var guidance = JsonNode.Parse(body)!["guidance"]![0]!["body"]!.GetValue<string>();
        guidance.ShouldContain(match.Key);
        guidance.ShouldNotContain(email);
        foreach (var text in new[] { "private-match-description-sentinel", "private-match-comment-sentinel", name })
        {
            body.ShouldNotContain(text);
        }
    }

    private sealed record Match(Guid Id, string Key, int Pull, string Sha);

    private sealed record OutboxRow(string DedupeKey, string? Body);

    private sealed record JobRow(string Key, string Payload);

    private async Task<(AgentDispatchArrange Arranged, Guid Item, GitHubIssueRecord Issue)> ArrangeAsync(
        int maxRounds = 5, bool connection = true, string title = "Login button says Sing in")
    {
        AgentDispatchArrange arranged;
        await using (var database = await OpenDatabaseAsync())
        {
            arranged = await AgentDispatchArrange.CreateAsync(database, Api, connection);
        }

        var item = (await arranged.CreateItemAsync(Api)).Id;
        var repository = $"shiplean-tests/rounds-{Guid.NewGuid():N}";
        var issue = await Host.Services.GetRequiredService<IGitHubIssueStore>().InsertAsync(
            new IssueRef(IssueRef.GitHubHost, repository, 1), arranged.Project.Id, item, "reporter", title, "The issue body.", IssuePhase.Proposing);
        await using (var database = await OpenDatabaseAsync())
        {
            var key = await database.QuerySingleAsync<string>("SELECT key FROM project WHERE id = @id", new { id = arranged.Project.Id });
            Host.Services.GetRequiredService<FixedIssueIntakeSettings>()
                .Take(new IssueIntakeProject(arranged.Project.Id, key, repository, SeedIds.Users.Joel, Instructions, maxRounds));
        }

        return (arranged, item, issue);
    }

    // A closed item of the project titled like the issue, with a merged github.com pull request, indexed for search.
    private async Task<Match> AddMatchAsync(AgentDispatchArrange arranged, string title)
    {
        var (id, key, _) = await arranged.CreateItemAsync(Api, arranged.Resolved);
        var pull = Random.Shared.Next(1, int.MaxValue);
        var sha = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray())[..12];
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE work_item SET title = @title WHERE id = @id", new { title, id });
        await database.ExecuteAsync(
            """
            INSERT INTO work_item_link (id, source_id, link_type, url, title, created_by, created_at)
            VALUES (@linkId, @id, 'url', @url, 'Pull request', @joel, now())
            """,
            new { linkId = Guid.NewGuid(), id, url = $"https://github.com/shiplean-tests/rounds/pull/{pull}", joel = SeedIds.Users.Joel });
        await database.ExecuteAsync(
            """
            INSERT INTO link_state (url_key, url, state, as_of, source, merge_commit_sha, merged_at)
            VALUES (@urlKey, @url, 'merged', now(), 'github', @sha, now())
            """,
            new { urlKey = $"github.com/shiplean-tests/rounds/pull/{pull}", url = $"https://github.com/shiplean-tests/rounds/pull/{pull}", sha });
        await IndexAsync();
        return new Match(id, key, pull, sha);
    }

    private async Task IndexAsync()
    {
        await using var scope = Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShipLean.Contracts.Search.ISearchIndexer>().RunPassAsync(true);
    }

    // RatePerHour round jobs of the project enqueued just now, already done, so none of them is a pending round of the issue.
    private async Task FillRateAsync(AgentDispatchArrange arranged, GitHubIssueRecord issue)
    {
        var jobs = Host.Services.GetRequiredService<IIssueJobs>();
        for (var i = 0; i < IssueProposalSessions.RatePerHour; i++)
        {
            await jobs.EnqueueAsync(IssueJobKind.Round, $"rate:{i}:{issue.Id}", issue.Id, arranged.Project.Id, null);
        }

        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE github_issue_id = @Id", new { issue.Id });
    }

    private async Task<RoundRequest> RequestAsync(Guid issueId, bool force)
    {
        await using var database = await OpenDatabaseAsync();
        await using var transaction = await database.BeginTransactionAsync();
        var outcome = await Sessions.RequestRoundAsync(issueId, force, transaction);
        await transaction.CommitAsync();
        return outcome;
    }

    // Polls pg_locks until the other connection waits on a lock; the losing request must not finish first.
    private async Task WaitUntilBlockedAsync(int pid, Task losing)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var probe = await OpenDatabaseAsync();
        while (!await probe.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = @pid AND NOT granted)", new { pid }, cancellationToken: timeout.Token)))
        {
            losing.IsCompleted.ShouldBeFalse("the second request finished without waiting for the first");
        }
    }

    private async Task<GitHubIssueRecord> IssueAsync(Guid id)
        => (await Host.Services.GetRequiredService<IGitHubIssueStore>().FindByIdAsync(id)).ShouldNotBeNull();

    private async Task SetRoundsAsync(Guid id, int rounds)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE github_issue SET rounds = @rounds WHERE id = @id", new { id, rounds });
    }

    private async Task SetPhaseAsync(Guid id, IssuePhase phase)
    {
        var from = Enum.GetValues<IssuePhase>();
        (await Host.Services.GetRequiredService<IGitHubIssueStore>().SetPhaseAsync(id, from, phase)).ShouldBeTrue();
    }

    private async Task<List<JobRow>> PendingRoundsAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<JobRow>(
            "SELECT key, payload::text AS payload FROM issue_job WHERE github_issue_id = @issueId AND kind = 'round' AND status = 'pending'", new { issueId })];
    }

    private async Task<List<string>> JobStatusesAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<string>("SELECT status FROM issue_job WHERE github_issue_id = @issueId ORDER BY created_at", new { issueId })];
    }

    private async Task<List<OutboxRow>> OutboxAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<OutboxRow>(
            "SELECT dedupe_key AS dedupeKey, body FROM github_outbox WHERE github_issue_id = @issueId ORDER BY seq", new { issueId })];
    }
}
