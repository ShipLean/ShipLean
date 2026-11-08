using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Application.Features.IssueIntake.Inbox;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Webhooks;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.IssueIntake.Replies;

// DbIssueReplies on the real stores and the real round service: every comment is mirrored once per GitHub URL, and a reply from
// the author or a maintainer asks for a round, which starts one, holds the issue, or reruns the round in flight. Each test
// arranges its own project, so the per-project rate limit never carries over. The teardown completes the tests' jobs.
[TestFixture]
public class RepliesTests : ApiFixture
{
    private const string Valid = """{ "verdict": "new", "type": "story", "title": "Fix the login label", "description": "The button says Sing in." }""";

    private readonly List<Guid> _issues = [];

    private readonly Dictionary<Guid, Guid?> _connections = [];

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<SwitchableIntakeSettings>();
        services.AddScoped<IIssueIntakeSettings>(sp => sp.GetRequiredService<SwitchableIntakeSettings>());
        services.AddSingleton<IssueProposalSessions>();
        services.AddSingleton<InterruptibleRounds>();
        services.AddSingleton<IIssueProposalSessions>(sp => sp.GetRequiredService<InterruptibleRounds>());
    };

    private IGitHubIssueStore Issues => Host.Services.GetRequiredService<IGitHubIssueStore>();

    private InterruptibleRounds Rounds => Host.Services.GetRequiredService<InterruptibleRounds>();

    [SetUp]
    public async Task ArrangeAsync()
    {
        Rounds.Before = null;
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE status = 'pending' AND kind = 'proposal'");
        await database.ExecuteAsync("UPDATE instance_settings SET base_url = 'https://shiplean.example.test'");
    }

    [TearDown]
    public async Task CompleteJobsAsync()
    {
        Rounds.Before = null;
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE status = 'pending' AND github_issue_id = ANY(@ids)", new { ids = _issues.ToArray() });
        _issues.Clear();
        _connections.Clear();
    }

    [Test]
    public async Task Reply_WhenAwaitingReply_ShouldMirrorAndStartRound()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply);
        var comment = Comment(issue, 1, "Could it filter by Feature too?");

        var outcome = await ReplyAsync(comment);

        outcome.Outcome.ShouldBe(WebhookOutcome.Processed);
        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
        (await IssueAsync(issue)).Phase.ShouldBe(IssuePhase.Proposing);
        (await PendingRoundJobsAsync(issue)).ShouldBe([IssueProposalSessions.JobKey(2, issue.Id)]);
    }

    [TestCase(IssuePhase.AlreadyShipped)]
    [TestCase(IssuePhase.WaitingForMaintainer)]
    public async Task Reply_WhenWaitingPhase_ShouldStartRound(IssuePhase phase)
    {
        var issue = await TakenAsync(phase);

        await ReplyAsync(Comment(issue, 1, "More detail"));

        (await IssueAsync(issue)).Phase.ShouldBe(IssuePhase.Proposing);
        (await PendingRoundJobsAsync(issue)).ShouldBe([IssueProposalSessions.JobKey(2, issue.Id)]);
    }

    [Test]
    public async Task Reply_WhenAtRoundCap_ShouldHoldWithNotices()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply, maxRounds: 1);

        await ReplyAsync(Comment(issue, 1, "One more thing"));

        (await IssueAsync(issue)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        (await PendingRoundJobsAsync(issue)).ShouldBeEmpty();
        (await OutboxKeysAsync(issue)).ShouldHaveSingleItem().ShouldStartWith($"hold:{issue.Id}:{RoundHolds.RoundCap}:");
        (await CommentBodiesAsync(issue, RoundHolds.AgentName)).ShouldHaveSingleItem().ShouldContain("maximum of 1");
    }

    // Before the round's session starts: its pending job covers the reply.
    [Test]
    public async Task Reply_WhenProposing_ShouldOnlyMirror()
    {
        var issue = await TakenAsync(IssuePhase.Proposing, pendingRound: true);
        var comment = Comment(issue, 1, "Also this");

        await ReplyAsync(comment);

        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
        (await PendingRoundJobsAsync(issue)).ShouldBe([IssueProposalSessions.JobKey(1, issue.Id)]);
        (await RerunRequestedAsync(issue)).ShouldBeFalse();
    }

    [Test]
    public async Task Reply_WhenProposingAfterSessionStarts_ShouldRequestRerun()
    {
        var issue = await TakenAsync(IssuePhase.Proposing, pendingRound: true);
        await Host.Services.GetRequiredService<IIssueEffects>().TryClaimAsync(
            IssueProposalSessions.SessionKey(IssueProposalSessions.JobKey(1, issue.Id)), issue.Id, Guid.CreateVersion7());

        await ReplyAsync(Comment(issue, 1, "Also this"));

        (await RerunRequestedAsync(issue)).ShouldBeTrue();
        (await PendingRoundJobsAsync(issue)).ShouldBe([IssueProposalSessions.JobKey(1, issue.Id)]);
    }

    [Test]
    public async Task Reply_WhenProposingAfterRoundJobDone_ShouldRequestRerunAndNoSecondRound()
    {
        var issue = await TakenAsync(IssuePhase.Proposing);

        await ReplyAsync(Comment(issue, 1, "Also this"));

        (await RerunRequestedAsync(issue)).ShouldBeTrue();
        (await IssueAsync(issue)).Rounds.ShouldBe(1);
        (await PendingRoundJobsAsync(issue)).ShouldBeEmpty();
    }

    [TestCase(IssuePhase.Accepting)]
    [TestCase(IssuePhase.Accepted)]
    [TestCase(IssuePhase.Closed)]
    public async Task Reply_WhenConversationOver_ShouldOnlyMirror(IssuePhase phase)
    {
        var issue = await TakenAsync(phase);
        var comment = Comment(issue, 1, "Thanks");

        await ReplyAsync(comment);

        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
        var after = await IssueAsync(issue);
        after.Phase.ShouldBe(phase);
        (await RerunRequestedAsync(issue)).ShouldBeFalse();
        (await PendingRoundJobsAsync(issue)).ShouldBeEmpty();
    }

    [Test]
    public async Task Reply_WhenConcurrentTwo_ShouldRequestOneRound()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply);
        var first = Comment(issue, 1, "One");
        var second = Comment(issue, 2, "Two");

        await Task.WhenAll(ReplyAsync(first), ReplyAsync(second));

        (await PendingRoundJobsAsync(issue)).ShouldBe([IssueProposalSessions.JobKey(2, issue.Id)]);
        (await MirroredAsync(first.CommentUrl)).ShouldBe(1);
        (await MirroredAsync(second.CommentUrl)).ShouldBe(1);
    }

    [Test]
    public async Task Reply_WhenReplayedAfterRoundFinished_ShouldNotStartAnotherRound()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply);
        var comment = Comment(issue, 1, "Again");
        await ReplyAsync(comment);
        await CompleteRoundJobsAsync(issue);
        await Issues.SetPhaseAsync(issue.Id, [IssuePhase.Proposing], IssuePhase.AwaitingReply);

        await ReplyAsync(comment);

        (await IssueAsync(issue)).Rounds.ShouldBe(2);
        (await PendingRoundJobsAsync(issue)).ShouldBeEmpty();
        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
    }

    [Test]
    public async Task Reply_WhenCrashAfterMirror_ShouldRequestRoundOnceOnRetry()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply);
        var comment = Comment(issue, 1, "Crash me");
        Rounds.Before = () => throw new InvalidOperationException("crash after the mirror");

        await Should.ThrowAsync<InvalidOperationException>(() => ReplyAsync(comment));

        (await MirroredAsync(comment.CommentUrl)).ShouldBe(0);
        (await PendingRoundJobsAsync(issue)).ShouldBeEmpty();

        Rounds.Before = null;
        await ReplyAsync(comment);
        await ReplyAsync(comment);

        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
        (await PendingRoundJobsAsync(issue)).ShouldBe([IssueProposalSessions.JobKey(2, issue.Id)]);
    }

    [Test]
    public async Task Reply_WhenIntakeOff_ShouldIgnoreAndWriteNothing()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply);
        Host.Services.GetRequiredService<SwitchableIntakeSettings>().Drop(issue.Issue.Repository);
        var comment = Comment(issue, 1, "Hello");

        (await ReplyAsync(comment)).ShouldBe(IssueOutcome.Ignored);

        (await MirroredAsync(comment.CommentUrl)).ShouldBe(0);
        (await IssueAsync(issue)).Phase.ShouldBe(IssuePhase.AwaitingReply);
    }

    [Test]
    public async Task Reply_WhenIntakeTurnsOffBeforeRound_ShouldMirrorWithNoRoundWrites()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply);
        var comment = Comment(issue, 1, "Hello");
        Rounds.Before = () => Host.Services.GetRequiredService<SwitchableIntakeSettings>().Drop(issue.Issue.Repository);

        await ReplyAsync(comment);

        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
        (await IssueAsync(issue)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await EffectExistsAsync($"reply:{issue.Id}:{comment.CommentId}")).ShouldBeFalse();
    }

    // The reply takes the project lock first, while the inbox's proposal waits behind it; the inbox then posts the proposal and
    // honours the rerun the reply asked for, with no deadlock and no second round from the reply itself.
    [Test]
    public async Task Reply_WhenProposalCompletesConcurrently_ShouldHonourRerun()
    {
        var issue = await TakenAsync(IssuePhase.Proposing);
        var sessionId = await ProposeSessionAsync(issue);
        await EnqueueProposalAsync(issue, sessionId);
        var comment = Comment(issue, 1, "While you were writing");

        await using var holder = await OpenDatabaseAsync();
        await using var held = await holder.BeginTransactionAsync();
        await holder.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended('issue_round:' || @ProjectId::text, 0))", new { issue.ProjectId }, held);
        var reply = ReplyAsync(comment);
        await WaitForRoundLockWaitersAsync(issue.ProjectId, 1, reply);
        var inbox = Host.Services.GetRequiredService<ProposalJobWorker>().RunNextAsync(CancellationToken.None);
        await WaitForRoundLockWaitersAsync(issue.ProjectId, 2, inbox);
        await held.CommitAsync();

        // A deadlock would fail one of them: Postgres's detector aborts a cycle with 40P01.
        await Task.WhenAll(reply, inbox);

        var after = await IssueAsync(issue);
        after.Phase.ShouldBe(IssuePhase.Proposing);
        (await RerunRequestedAsync(issue)).ShouldBeFalse();
        after.ProposalSessionId.ShouldBe(sessionId);
        (await PendingRoundJobsAsync(issue)).ShouldBe([IssueProposalSessions.JobKey(2, issue.Id)]);
        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
    }

    // The inbox holds the project lock and waits on the issue row; the reply must queue on the project lock, holding no row the
    // inbox needs. Then both finish: the proposal is posted and the reply starts the next round.
    [Test]
    public async Task Reply_WhenInboxHoldsProjectLock_ShouldWaitOnItBeforeAnyRow()
    {
        var issue = await TakenAsync(IssuePhase.Proposing);
        var sessionId = await ProposeSessionAsync(issue);
        await EnqueueProposalAsync(issue, sessionId);
        var comment = Comment(issue, 1, "While you were posting");

        await using var holder = await OpenDatabaseAsync();
        var holderPid = holder.ProcessID;
        await using var held = await holder.BeginTransactionAsync();
        await holder.ExecuteAsync("SELECT 1 FROM github_issue WHERE id = @Id FOR UPDATE", new { issue.Id }, held);
        var inbox = Host.Services.GetRequiredService<ProposalJobWorker>().RunNextAsync(CancellationToken.None);
        await WaitUntilBlockedByAsync(holderPid, inbox);
        var reply = ReplyAsync(comment);
        await WaitForRoundLockWaitersAsync(issue.ProjectId, 1, reply);
        await held.CommitAsync();

        // A deadlock would fail one of them: Postgres's detector aborts a cycle with 40P01.
        await Task.WhenAll(reply, inbox);

        var after = await IssueAsync(issue);
        after.Phase.ShouldBe(IssuePhase.Proposing);
        after.ProposalSessionId.ShouldBe(sessionId);
        (await PendingRoundJobsAsync(issue)).ShouldBe([IssueProposalSessions.JobKey(2, issue.Id)]);
    }

    [Test]
    public async Task Reply_WhenNoRecord_ShouldIgnore()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply);
        var unknown = issue.Issue with { Number = issue.Issue.Number + 1 };

        (await ReplyAsync(Comment(unknown, 1, "Hi"))).ShouldBe(IssueOutcome.Ignored);
    }

    [Test]
    public async Task Mirror_WhenReplayed_ShouldAddOnce()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply);
        var comment = Comment(issue, 1, "Me too");

        await MirrorAsync(comment);
        await MirrorAsync(comment);

        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
        (await CommentBodiesAsync(issue, "GitHub @reporter")).Count(b => b.Contains("Me too", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Test]
    public async Task Mirror_ShouldNotStartRound()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply);

        await MirrorAsync(Comment(issue, 1, "Passer-by"));

        (await IssueAsync(issue)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await PendingRoundJobsAsync(issue)).ShouldBeEmpty();
    }

    [Test]
    public async Task Mirror_WhenBodyTooLong_ShouldCapIt()
    {
        var issue = await TakenAsync(IssuePhase.AwaitingReply);
        var comment = Comment(issue, 1, new string('x', IssueEvent.MaxTextBytes + 10));

        await MirrorAsync(comment);

        await using var connection = await OpenDatabaseAsync();
        var stored = await connection.ExecuteScalarAsync<string>("SELECT body FROM github_issue_comment WHERE html_url = @url", new { url = comment.CommentUrl });
        stored!.ShouldEndWith(IssueEvent.TruncatedNote);
    }

    private static IssueCommentEvent Comment(IssueRef issue, long id, string body)
    {
        return new(issue, $"https://github.com/{issue.Repository}/issues/{issue.Number}", "Title", "reporter", true, false, id,
            $"https://github.com/{issue.Repository}/issues/{issue.Number}#issuecomment-{id}", body, "reporter", "NONE", false, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
    }

    private static IssueCommentEvent Comment(GitHubIssueRecord issue, long id, string body)
    {
        return Comment(issue.Issue, id, body);
    }

    // An issue of a project of its own that had round 1 (its job done, or still pending), now in the given phase.
    private async Task<GitHubIssueRecord> TakenAsync(IssuePhase phase, int maxRounds = 5, bool pendingRound = false)
    {
        AgentDispatchArrange arranged;
        await using (var database = await OpenDatabaseAsync())
        {
            arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        }

        var item = (await arranged.CreateItemAsync(Api)).Id;
        var repository = $"shiplean-tests/replies-{Guid.NewGuid():N}";
        var issue = await Issues.InsertAsync(
            new IssueRef(IssueRef.GitHubHost, repository, 1), arranged.Project.Id, item, "reporter", "Login button says Sing in", "The issue body.", phase);
        _issues.Add(issue.Id);
        _connections[issue.Id] = arranged.ConnectionId;
        await using (var database = await OpenDatabaseAsync())
        {
            var key = await database.QuerySingleAsync<string>("SELECT key FROM project WHERE id = @id", new { id = arranged.Project.Id });
            Host.Services.GetRequiredService<SwitchableIntakeSettings>()
                .Take(new IssueIntakeProject(arranged.Project.Id, key, repository, SeedIds.Users.Joel, "Write a proposal.", maxRounds));
            await database.ExecuteAsync("UPDATE github_issue SET rounds = 1 WHERE id = @Id", new { issue.Id });
        }

        (await Host.Services.GetRequiredService<IIssueJobs>().EnqueueAsync(
            IssueJobKind.Round, IssueProposalSessions.JobKey(1, issue.Id), issue.Id, issue.ProjectId, JsonSerializer.Serialize(new RoundJobPayload(1)))).ShouldBeTrue();
        if (!pendingRound)
        {
            await CompleteRoundJobsAsync(issue);
        }

        return await IssueAsync(issue);
    }

    // The propose session whose proposal the inbox posts, made the issue's proposal session as the round worker's fence does.
    private async Task<Guid> ProposeSessionAsync(GitHubIssueRecord issue)
    {
        var sessionId = Guid.CreateVersion7();
        await using var database = await OpenDatabaseAsync();
        var connectionId = _connections[issue.Id];
        await database.ExecuteAsync(
            """
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, created_by, created_at, mode, ended_at)
            VALUES (@sessionId, @WorkItemId, @connectionId, 'completed', 'https://github.com/shiplean-tests/replies', 'main', now(), @joel, now(), 'propose', now())
            """,
            new { sessionId, issue.WorkItemId, connectionId, joel = SeedIds.Users.Joel });
        await database.ExecuteAsync("UPDATE github_issue SET proposal_session_id = @sessionId WHERE id = @Id", new { sessionId, issue.Id });
        return sessionId;
    }

    private async Task EnqueueProposalAsync(GitHubIssueRecord issue, Guid sessionId)
    {
        var body = $"A fix is proposed.\n\n```shiplean-proposal\n{Valid}\n```\n";
        (await Host.Services.GetRequiredService<IIssueJobs>().EnqueueAsync(
            IssueJobKind.Proposal, $"proposal:{sessionId}", issue.Id, issue.ProjectId, JsonSerializer.Serialize(new { sessionId, body }))).ShouldBeTrue();
    }

    // Polls pg_locks until count backends wait on the project's round lock; the waiting task must not finish first.
    private async Task WaitForRoundLockWaitersAsync(Guid projectId, int count, Task waiting)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var probe = await OpenDatabaseAsync();
        while (await probe.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM pg_locks
            WHERE locktype = 'advisory' AND NOT granted AND objsubid = 1
              AND ((classid::bigint << 32) | objid::bigint) = hashtextextended('issue_round:' || @projectId::text, 0)
            """,
            new { projectId },
            cancellationToken: timeout.Token)) < count)
        {
            waiting.IsCompleted.ShouldBeFalse("it finished without waiting on the project's round lock");
        }
    }

    // Polls pg_stat_activity until a backend waits on a lock that blocker holds; the paused task must not finish first.
    private async Task WaitUntilBlockedByAsync(int blocker, Task paused)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var probe = await OpenDatabaseAsync();
        while (!await probe.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE @blocker = ANY(pg_blocking_pids(pid)))", new { blocker }, cancellationToken: timeout.Token)))
        {
            paused.IsCompleted.ShouldBeFalse("the inbox finished without waiting on the issue row");
        }
    }

    private async Task<IssueOutcome> ReplyAsync(IssueCommentEvent comment)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIssueReplies>().ReplyAsync(comment, CancellationToken.None);
    }

    private async Task<IssueOutcome> MirrorAsync(IssueCommentEvent comment)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIssueReplies>().MirrorAsync(comment, CancellationToken.None);
    }

    private async Task<GitHubIssueRecord> IssueAsync(GitHubIssueRecord issue)
    {
        return (await Issues.FindByIdAsync(issue.Id)).ShouldNotBeNull();
    }

    private async Task CompleteRoundJobsAsync(GitHubIssueRecord issue)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE github_issue_id = @Id AND kind = 'round'", new { issue.Id });
    }

    private async Task<List<string>> PendingRoundJobsAsync(GitHubIssueRecord issue)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<string>(
            "SELECT key FROM issue_job WHERE github_issue_id = @Id AND kind = 'round' AND status = 'pending' ORDER BY created_at", new { issue.Id })];
    }

    private async Task<bool> RerunRequestedAsync(GitHubIssueRecord issue)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<bool>("SELECT rerun_requested FROM github_issue WHERE id = @Id", new { issue.Id });
    }

    private async Task<bool> EffectExistsAsync(string key)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM github_issue_effect WHERE key = @key)", new { key });
    }

    private async Task<int> MirroredAsync(string url)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM github_issue_comment WHERE html_url = @url", new { url });
    }

    private async Task<List<string>> OutboxKeysAsync(GitHubIssueRecord issue)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<string>("SELECT dedupe_key FROM github_outbox WHERE github_issue_id = @Id ORDER BY seq", new { issue.Id })];
    }

    private async Task<List<string>> CommentBodiesAsync(GitHubIssueRecord issue, string agentName)
    {
        await using var connection = await OpenDatabaseAsync();
        return [.. await connection.QueryAsync<string>(
            "SELECT body FROM comment WHERE work_item_id = @WorkItemId AND agent_name = @agentName ORDER BY created_at", new { issue.WorkItemId, agentName })];
    }

    // The real round service, with a hook that runs before each request: a crash, or intake turning off mid-reply.
    private sealed class InterruptibleRounds(IssueProposalSessions inner) : IIssueProposalSessions
    {
        public Action? Before { get; set; }

        public Task<RoundRequest> RequestRoundAsync(Guid issueId, bool force, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            Before?.Invoke();
            return inner.RequestRoundAsync(issueId, force, transaction, cancellationToken);
        }

        public Task LockProjectAsync(Guid projectId, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            return inner.LockProjectAsync(projectId, transaction, cancellationToken);
        }
    }

    // Intake settings a test can turn off for one repository.
    private sealed class SwitchableIntakeSettings : IIssueIntakeSettings
    {
        private readonly ConcurrentDictionary<string, IssueIntakeProject> _projects = new(StringComparer.OrdinalIgnoreCase);

        public void Take(IssueIntakeProject project)
        {
            _projects[project.Repository] = project;
        }

        public void Drop(string repository)
        {
            _projects.TryRemove(repository, out _);
        }

        public Task<IssueIntakeProject?> ForRepositoryAsync(IssueRef issue, CancellationToken cancellationToken = default)
        {
            var project = string.Equals(issue.Host, IssueRef.GitHubHost, StringComparison.OrdinalIgnoreCase) && _projects.TryGetValue(issue.Repository, out var found)
                ? found
                : null;
            return Task.FromResult(project);
        }
    }
}
