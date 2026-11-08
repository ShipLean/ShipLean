using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.IssueIntake.Acceptance;
using ShipLean.Application.Features.IssueIntake.Inbox;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Application.Features.IssueIntake.Progress;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Webhooks;
using ShipLean.TestSupport;
using ShipLean.TestSupport.IssueIntake;
using ShipLean.TestSupport.Webhooks;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.WebApi.Tests.IssueIntake.InboxWorker;
using ShipLean.WebApi.Tests.Webhooks;

namespace ShipLean.WebApi.Tests.IssueIntake.Journeys;

// End-to-end failure and recovery journeys for GitHub issue conversations (SHIP-742): signed deliveries through the real webhook
// endpoint, real settings saved through the API, the real propose session start and agent report path answering with the golden
// proposals, the real outbox and workers driven by hand (the loops are off in the Test profile), and a recording GitHub. Each test
// takes intake for acme/storefront with a project of its own (one enabled project per repository) and uses its own issue number.
[TestFixture]
public class FailureJourneyTests : WebhookApiTest
{
    private const string Stranger = "sam-stranger";
    private const string Instance = "https://shiplean.example.test";
    private const string Marker = "Done `<!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001 -->` here.";

    private static readonly string Goldens = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "IssueIntake", "Proposals");

    private long _commentIds = 2_470_000_000;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [WebhookSecrets.GithubKey] = GithubSecret };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<RecordingGitHubIssues>();
        services.AddSingleton<JourneyGitHub>();
        services.AddSingleton<IGitHubIssues>(sp => sp.GetRequiredService<JourneyGitHub>());
        services.AddSingleton<IGitHubIdentity>(new FixedGitHubIdentity());
        services.AddSingleton<DbGitHubOutbox>();
        services.AddSingleton<CrashingGitHubOutbox>();
        services.AddSingleton<IGitHubOutbox>(sp => sp.GetRequiredService<CrashingGitHubOutbox>());
    };

    private RecordingGitHubIssues GitHub => Host.Services.GetRequiredService<RecordingGitHubIssues>();

    private JourneyGitHub Wire => Host.Services.GetRequiredService<JourneyGitHub>();

    private CrashingGitHubOutbox Outbox => Host.Services.GetRequiredService<CrashingGitHubOutbox>();

    [SetUp]
    [TearDown]
    public async Task ResetAsync()
    {
        Outbox.CrashAfterEnqueue = false;
        Wire.LandThenTimeOut = false;
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE status = 'pending'");
        await database.ExecuteAsync("UPDATE github_outbox SET failed_at = now() WHERE sent_at IS NULL AND failed_at IS NULL");
        await database.ExecuteAsync("UPDATE instance_settings SET base_url = @Instance", new { Instance });
    }

    [Test]
    public async Task GitHubIssue_WhenRelayBlocked_ShouldHoldAndResumeOnSlashShiplean()
    {
        var journey = await ArrangeAsync();
        await SetConnectionAsync(journey, null);
        var issue = await OpenAsync(701);

        await DrainAsync();

        (await IssueAsync(701)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        (await SentBodiesAsync(issue.Id)).ShouldBe([RoundHolds.Comment]);
        (await NotesAsync(issue.WorkItemId)).ShouldHaveSingleItem().ShouldContain("could not start");

        // A stranger can't use /shiplean: it is only mirrored.
        await CommentAsync(701, "/shiplean", Stranger, "NONE");
        await DrainAsync();
        (await IssueAsync(701)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        (await MirroredAsync(issue.Id)).ShouldBe([$"{Stranger}: /shiplean"]);

        await SetConnectionAsync(journey, journey.Arranged.ConnectionId);
        (await CommentAsync(701, "/shiplean", WebhookDelivery.Maintainer, "MEMBER")).Outcome.ShouldBe(WebhookOutcome.Processed);
        await DrainAsync();
        (await IssueAsync(701)).Phase.ShouldBe(IssuePhase.Proposing);

        await RespondAsync(journey, 701, Golden("new.md"));
        await DrainAsync();

        (await IssueAsync(701)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        var sent = await SentBodiesAsync(issue.Id);
        sent.Count.ShouldBe(2);
        sent[1].ShouldStartWith("Thanks for the report.");
        CommentCalls(701).ShouldBe(2);
    }

    [Test]
    public async Task GitHubIssue_WhenRateExceeded_ShouldHold()
    {
        var journey = await ArrangeAsync();
        var first = await OpenAsync(702);
        await DrainAsync();
        await FillRateAsync(journey, first);

        var held = await OpenAsync(703);
        await DrainAsync();

        (await IssueAsync(703)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        (await SentBodiesAsync(held.Id)).ShouldBe([RoundHolds.Comment]);

        // A maintainer's /shiplean skips the rate limit.
        await CommentAsync(703, "/shiplean", WebhookDelivery.Maintainer, "OWNER");
        await DrainAsync();

        var resumed = await IssueAsync(703);
        resumed.Phase.ShouldBe(IssuePhase.Proposing);
        resumed.ProposalSessionId.ShouldNotBeNull();
        (await SentBodiesAsync(held.Id)).ShouldBe([RoundHolds.Comment]);
    }

    [Test]
    public async Task GitHubIssue_WhenRoundCapReached_ShouldHoldAndResumeOnSlashShiplean()
    {
        var journey = await ArrangeAsync(maxRounds: 1);
        var issue = await OpenAsync(704);
        await ProposeAsync(journey, 704, Golden("new.md"));

        await CommentAsync(704, "Could it cover Features too?", WebhookDelivery.IssueAuthor, "NONE");
        await DrainAsync();

        (await IssueAsync(704)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        (await SentBodiesAsync(issue.Id))[^1].ShouldBe(RoundHolds.Comment);

        await CommentAsync(704, "/shiplean", WebhookDelivery.Maintainer, "COLLABORATOR");
        await DrainAsync();

        var resumed = await IssueAsync(704);
        resumed.Phase.ShouldBe(IssuePhase.Proposing);
        resumed.Rounds.ShouldBe(2);
    }

    [Test]
    public async Task GitHubIssue_WhenProposalMalformed_ShouldNotPost()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(705);

        await ProposeAsync(journey, 705, Golden("badJson.md"));

        (await IssueAsync(705)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
        CommentCalls(705).ShouldBe(0);
        (await NotesAsync(issue.WorkItemId)).ShouldHaveSingleItem().ShouldContain("could not be read");
    }

    [Test]
    public async Task GitHubIssue_WhenProposalUnsafe_ShouldHoldWithoutPostingIt()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(706);

        await ProposeAsync(journey, 706, Golden("new.md").Replace("Thanks for the report.", Marker, StringComparison.Ordinal));

        var held = await IssueAsync(706);
        held.Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        held.Proposal.ShouldNotBeNull();
        (await SentBodiesAsync(issue.Id)).ShouldBe([RoundHolds.Comment]);
        CommentCalls(706).ShouldBe(1);
    }

    [Test]
    public async Task GitHubIssue_WhenBaseUrlBlank_ShouldHold()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(707);
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE instance_settings SET base_url = ''");
        }

        await ProposeAsync(journey, 707, Golden("new.md"));

        (await IssueAsync(707)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
        (await NotesAsync(issue.WorkItemId)).ShouldHaveSingleItem().ShouldStartWith(ProposalJobWorker.NoBaseUrlNote);
    }

    [Test]
    public async Task GitHubIssue_WhenDuplicate_ShouldRepoint()
    {
        var journey = await ArrangeAsync();
        var existing = await journey.Arranged.CreateItemAsync(Api);
        var issue = await OpenAsync(708);
        await ProposeAsync(journey, 708, Golden("duplicate.md").Replace("SHIP-300", existing.Key, StringComparison.Ordinal));

        await CommentAsync(708, "LGTM", WebhookDelivery.IssueAuthor, "NONE");
        await DrainAsync();

        var accepted = await IssueAsync(708);
        accepted.Phase.ShouldBe(IssuePhase.Accepted);
        accepted.WorkItemId.ShouldBe(existing.Id);
        (await StateNameAsync(issue.WorkItemId)).ShouldBe("Removed");
    }

    [Test]
    public async Task GitHubIssue_WhenStrangerSaysLgtm_ShouldOnlyMirror()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(709);
        await ProposeAsync(journey, 709, Golden("new.md"));

        (await CommentAsync(709, "LGTM", Stranger, "NONE")).Outcome.ShouldBe(WebhookOutcome.Processed);
        await CommentAsync(709, $"@{WebhookDelivery.IssueAuthor} says LGTM", Stranger, "CONTRIBUTOR");
        await DrainAsync();

        var stored = await IssueAsync(709);
        stored.Phase.ShouldBe(IssuePhase.AwaitingReply);
        stored.Rounds.ShouldBe(1);
        (await MirroredAsync(issue.Id)).ShouldBe([$"{Stranger}: LGTM", $"{Stranger}: @{WebhookDelivery.IssueAuthor} says LGTM"]);
        (await PendingJobsAsync(issue.Id)).ShouldBeEmpty();
        CommentCalls(709).ShouldBe(1);
    }

    [Test]
    public async Task GitHubIssue_WhenLgtmOlderThanProposal_ShouldOnlyMirror()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(710);
        await ProposeAsync(journey, 710, Golden("new.md"));

        // Comment ids grow on GitHub: an id below the proposal's was written before the proposal appeared.
        await CommentAsync(710, "LGTM", WebhookDelivery.IssueAuthor, "NONE", commentId: 1);
        await DrainAsync();

        (await IssueAsync(710)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await MirroredAsync(issue.Id)).ShouldBe([$"{WebhookDelivery.IssueAuthor}: LGTM"]);
        (await PendingJobsAsync(issue.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task GitHubIssue_WhenGitHubFailsThenRecovers_ShouldPostOnce()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(711);
        GitHub.QueueResult(new GitHubWriteResult.Retry("rate limited"));

        await ProposeAsync(journey, 711, Golden("new.md"));

        var waiting = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        waiting.SentAt.ShouldBeNull();
        waiting.LastResult.ShouldBe("retry");

        await MakeOutboxDueAsync(issue.Id);
        await DrainAsync();

        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().SentAt.ShouldNotBeNull();
        (await LandedAsync(711)).ShouldBe(1);
    }

    [Test]
    public async Task GitHubIssue_WhenCommentPostTimesOutButLanded_ShouldNotPostTwice()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(712);
        Wire.LandThenTimeOut = true;

        await ProposeAsync(journey, 712, Golden("new.md"));
        await MakeOutboxDueAsync(issue.Id);
        await DrainAsync();

        var row = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        row.SentAt.ShouldNotBeNull();
        row.GitHubCommentId.ShouldNotBeNull();
        (await LandedAsync(712)).ShouldBe(1);
        CommentCalls(712).ShouldBe(1);
    }

    [Test]
    public async Task GitHubIssue_WhenClosedAndReopened_ShouldPauseAndResume()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(713);
        await ProposeAsync(journey, 713, Golden("new.md"));

        await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "closed", 713, state: "closed"));
        await CommentAsync(713, "LGTM", WebhookDelivery.IssueAuthor, "NONE");
        await CommentAsync(713, "Actually, one more thing.", WebhookDelivery.IssueAuthor, "NONE");
        await DrainAsync();

        var closed = await IssueAsync(713);
        closed.OpenOnGitHub.ShouldBeFalse();
        closed.Phase.ShouldBe(IssuePhase.AwaitingReply);
        closed.Rounds.ShouldBe(1);
        (await PendingJobsAsync(issue.Id)).ShouldBeEmpty();
        CommentCalls(713).ShouldBe(1);

        await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "reopened", 713));
        await CommentAsync(713, "Please also cover Features.", WebhookDelivery.IssueAuthor, "NONE");
        await DrainAsync();

        var resumed = await IssueAsync(713);
        resumed.OpenOnGitHub.ShouldBeTrue();
        resumed.Phase.ShouldBe(IssuePhase.Proposing);
        resumed.Rounds.ShouldBe(2);
    }

    [Test]
    public async Task GitHubIssue_WhenRemoved_ShouldCloseNotPlanned()
    {
        var journey = await ArrangeAsync();
        var issue = await AcceptNewAsync(journey, 714);

        await RemoveAsync(journey, issue);
        await Host.Services.GetRequiredService<ProgressReconciler>().ReconcileAsync(issue.Id, CancellationToken.None);
        await DrainAsync();

        GitHubCalls(714).TakeLast(2).ShouldBe([$"comment {Ref(714)}", $"close {Ref(714)} notPlanned"]);
    }

    [Test]
    public async Task GitHubIssue_WhenRemovedMessageFails_ShouldHoldClose()
    {
        var journey = await ArrangeAsync();
        var issue = await AcceptNewAsync(journey, 715);
        var before = GitHubCalls(715).Count;

        await RemoveAsync(journey, issue);
        await Host.Services.GetRequiredService<ProgressReconciler>().ReconcileAsync(issue.Id, CancellationToken.None);
        GitHub.QueueResult(new GitHubWriteResult.Failed("422 Unprocessable"));
        await DrainAsync();

        GitHubCalls(715).Skip(before).ShouldBe([$"comment {Ref(715)}"]);
        var rows = await OutboxAsync(issue.Id);
        rows.ShouldContain(r => r.Kind == "close" && r.SentAt == null);
        (await NotesAsync(issue.WorkItemId)).ShouldContain(n => n.Contains("close", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task GitHubIssue_WhenBotComments_ShouldIgnore()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(716);
        await ProposeAsync(journey, 716, Golden("new.md"));

        var bot = await CommentAsync(716, "LGTM", "helper-app[bot]", "MEMBER", authorType: "Bot");
        var self = await CommentAsync(716, "LGTM", FixedGitHubIdentity.DefaultLogin, "MEMBER");
        await DrainAsync();

        bot.Outcome.ShouldBe(WebhookOutcome.Ignored);
        self.Outcome.ShouldBe(WebhookOutcome.Ignored);
        (await IssueAsync(716)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await MirroredAsync(issue.Id)).ShouldBeEmpty();
        (await PendingJobsAsync(issue.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task GitHubIssue_WhenLgtmDuringNewRound_ShouldSayBusyThenAcceptLatest()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(717);
        await ProposeAsync(journey, 717, Golden("new.md"));
        await CommentAsync(717, "Make it labels, not tags.", WebhookDelivery.IssueAuthor, "NONE");
        await DrainAsync();
        (await IssueAsync(717)).Phase.ShouldBe(IssuePhase.Proposing);

        await CommentAsync(717, "LGTM", WebhookDelivery.IssueAuthor, "NONE");
        await DrainAsync();
        (await SentBodiesAsync(issue.Id))[^1].ShouldBe(AcceptanceTexts.Busy);

        await RespondAsync(journey, 717, Golden("new.md").Replace("by tag", "by label", StringComparison.Ordinal));
        await DrainAsync();
        await CommentAsync(717, "LGTM", WebhookDelivery.IssueAuthor, "NONE");
        await DrainAsync();

        (await IssueAsync(717)).Phase.ShouldBe(IssuePhase.Accepted);
        (await TitleAsync(issue.WorkItemId)).ShouldBe("Filter the backlog by label");
    }

    [Test]
    public async Task GitHubIssue_WhenSameDeliveryTwiceAtOnce_ShouldActOnce()
    {
        await ArrangeAsync();
        var opened = WebhookDelivery.GithubIssue(GithubSecret, "opened", 718);

        await Task.WhenAll(DeliverAsync(opened), DeliverAsync(opened));

        var issue = await IssueAsync(718);
        await using var database = await OpenDatabaseAsync();
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM github_issue WHERE number = 718")).ShouldBe(1);
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM work_item WHERE project_id = @ProjectId", new { issue.ProjectId })).ShouldBe(1);
        (await PendingJobsAsync(issue.Id)).ShouldBe(["round"]);
    }

    private async Task<Journey> ArrangeAsync(int maxRounds = 5)
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        await database.ExecuteAsync("UPDATE project_issue_intake SET enabled = false");
        await database.ExecuteAsync("UPDATE project SET repo_url = 'https://github.com/Acme/Storefront' WHERE id = @Id", new { arranged.Project.Id });
        using var saved = await Api.PutJsonAsync(
            $"api/projects/{arranged.Project.Id}/settings/issue-intake",
            new { enabled = true, runAsUserId = SeedIds.Users.Joel, proposalInstructions = "", maxRounds });
        saved.IsSuccessStatusCode.ShouldBeTrue(await saved.Content.ReadAsStringAsync());
        return new Journey(arranged);
    }

    private async Task SetConnectionAsync(Journey journey, Guid? connectionId)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE project SET agent_connection_id = @connectionId WHERE id = @Id", new { connectionId, journey.Arranged.Project.Id });
    }

    private async Task<GitHubIssueRecord> OpenAsync(int number)
    {
        (await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "opened", number))).Outcome.ShouldBe(WebhookOutcome.Processed);
        return await IssueAsync(number);
    }

    private Task<WebhookResultDto> CommentAsync(int number, string body, string author, string association, long? commentId = null, string authorType = "User")
    {
        return DeliverAsync(WebhookDelivery.GithubIssueComment(
            GithubSecret, body, author, association, number, authorType: authorType, commentId: commentId ?? Interlocked.Increment(ref _commentIds)));
    }

    // Runs the round, answers the propose session with the response, and processes and delivers it.
    private async Task ProposeAsync(Journey journey, int number, string response)
    {
        await DrainAsync();
        await RespondAsync(journey, number, response);
        await DrainAsync();
    }

    private async Task RespondAsync(Journey journey, int number, string response)
    {
        var sessionId = (await IssueAsync(number)).ProposalSessionId.ShouldNotBeNull();
        await using var scope = Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAgentReports>().CreateActivityAsync(
            new AgentReporter(journey.Arranged.ConnectionId!.Value, "Relay"),
            new AgentActivityCreate(sessionId, "response", response, null, null, null, false, null, null));
    }

    private async Task<GitHubIssueRecord> AcceptNewAsync(Journey journey, int number)
    {
        await OpenAsync(number);
        await ProposeAsync(journey, number, Golden("new.md"));
        await CommentAsync(number, "LGTM", WebhookDelivery.IssueAuthor, "NONE");
        await DrainAsync();
        var accepted = await IssueAsync(number);
        accepted.Phase.ShouldBe(IssuePhase.Accepted);
        return accepted;
    }

    private async Task RemoveAsync(Journey journey, GitHubIssueRecord issue)
    {
        await using var database = await OpenDatabaseAsync();
        var revision = await database.ExecuteScalarAsync<int>("SELECT revision FROM work_item WHERE id = @WorkItemId", new { issue.WorkItemId });
        await AgentDispatchArrange.MoveAsync(Api, issue.WorkItemId, revision, journey.Arranged.Project.State(WorkItemTypeId.Story, "Removed"));
    }

    // Every worker until none has work left: rounds, proposals, accepts and the outbox.
    private async Task DrainAsync()
    {
        var rounds = Host.Services.GetRequiredService<RoundJobWorker>();
        var proposals = Host.Services.GetRequiredService<ProposalJobWorker>();
        var accepts = Host.Services.GetRequiredService<AcceptJobWorker>();
        var deliverer = Host.Services.GetRequiredService<GitHubOutboxDeliverer>();
        for (var pass = 0; pass < 20; pass++)
        {
            var worked = await rounds.RunNextAsync(CancellationToken.None);
            worked |= await proposals.RunNextAsync(CancellationToken.None);
            worked |= await accepts.RunNextAsync(CancellationToken.None);
            worked |= await deliverer.RunDueAsync(CancellationToken.None) > 0;
            if (!worked)
            {
                return;
            }
        }

        throw new InvalidOperationException("The workers still had work after 20 passes.");
    }

    private async Task FillRateAsync(Journey journey, GitHubIssueRecord issue)
    {
        var jobs = Host.Services.GetRequiredService<IIssueJobs>();
        for (var i = 0; i < IssueProposalSessions.RatePerHour; i++)
        {
            await jobs.EnqueueAsync(IssueJobKind.Round, $"rate:{i}:{issue.Id}", issue.Id, journey.Arranged.Project.Id, null);
        }

        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE github_issue_id = @Id AND status = 'pending'", new { issue.Id });
    }

    private async Task MakeOutboxDueAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            "UPDATE github_outbox SET next_attempt_at = now(), leased_until = NULL WHERE github_issue_id = @issueId AND sent_at IS NULL AND failed_at IS NULL", new { issueId });
    }

    private async Task<GitHubIssueRecord> IssueAsync(int number)
    {
        return (await Host.Services.GetRequiredService<IGitHubIssueStore>().FindAsync(Ref(number))).ShouldNotBeNull();
    }

    private static IssueRef Ref(int number) => new(IssueRef.GitHubHost, WebhookDelivery.GithubRepository, number);

    private static string Golden(string name) => File.ReadAllText(Path.Combine(Goldens, name));

    private IReadOnlyList<string> GitHubCalls(int number) =>
        [.. GitHub.Calls.Where(c => c.EndsWith($"#{number}", StringComparison.Ordinal) || c.Contains($"#{number} ", StringComparison.Ordinal))];

    private int CommentCalls(int number) => GitHubCalls(number).Count(c => c.StartsWith("comment ", StringComparison.Ordinal));

    // The comments on the recording GitHub's issue now (the read records a list call, so a test reads it last).
    private async Task<int> LandedAsync(int number)
    {
        var listed = (await GitHub.ListRecentCommentsAsync(Ref(number), DateTimeOffset.MinValue)).ShouldBeOfType<GitHubCommentList.Listed>();
        return listed.Comments.Count;
    }

    private async Task<List<OutboxRow>> OutboxAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<OutboxRow>(
            """
            SELECT kind, dedupe_key AS dedupeKey, body, sent_at AS sentAt, github_comment_id AS gitHubCommentId, last_result AS lastResult
            FROM github_outbox WHERE github_issue_id = @issueId ORDER BY seq
            """,
            new { issueId })];
    }

    private async Task<List<string>> SentBodiesAsync(Guid issueId) =>
        [.. (await OutboxAsync(issueId)).Where(r => r.SentAt is not null && r.Kind == "comment").Select(r => StripMarker(r.Body!))];

    private static string StripMarker(string body)
    {
        var at = body.IndexOf("\n\n<!-- shiplean:outbox:", StringComparison.Ordinal);
        return at < 0 ? body : body[..at];
    }

    private async Task<List<string>> MirroredAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<string>(
            "SELECT author_login || ': ' || body FROM github_issue_comment WHERE github_issue_id = @issueId ORDER BY github_comment_id", new { issueId })];
    }

    private async Task<List<string>> PendingJobsAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<string>("SELECT kind FROM issue_job WHERE github_issue_id = @issueId AND status = 'pending' ORDER BY created_at", new { issueId })];
    }

    // The ShipLean notes intake wrote on the item (agent name ShipLean).
    private async Task<List<string>> NotesAsync(Guid workItemId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<string>(
            "SELECT body FROM comment WHERE work_item_id = @workItemId AND agent_name = 'ShipLean' ORDER BY created_at", new { workItemId })];
    }

    private async Task<string> StateNameAsync(Guid workItemId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<string>("SELECT s.name FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.id = @workItemId", new { workItemId });
    }

    private async Task<string> TitleAsync(Guid workItemId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<string>("SELECT title FROM work_item WHERE id = @workItemId", new { workItemId });
    }

    private sealed record Journey(AgentDispatchArrange Arranged);

    private sealed record OutboxRow(string Kind, string DedupeKey, string? Body, DateTimeOffset? SentAt, long? GitHubCommentId, string? LastResult);

    // The recording GitHub, except that while LandThenTimeOut is set the next comment lands on GitHub and its POST still reports
    // Ambiguous, as a timeout after GitHub took the body does.
    private sealed class JourneyGitHub(RecordingGitHubIssues inner) : IGitHubIssues
    {
        public bool LandThenTimeOut { get; set; }

        public async Task<GitHubWriteResult> CommentAsync(IssueRef issue, string body, CancellationToken cancellationToken = default)
        {
            var result = await inner.CommentAsync(issue, body, cancellationToken);
            if (!LandThenTimeOut)
            {
                return result;
            }

            LandThenTimeOut = false;
            return new GitHubWriteResult.Ambiguous("The request timed out after the body was sent.");
        }

        public Task<GitHubWriteResult> CloseAsync(IssueRef issue, CloseReason reason, CancellationToken cancellationToken = default) =>
            inner.CloseAsync(issue, reason, cancellationToken);

        public Task<GitHubWriteResult> ReopenAsync(IssueRef issue, CancellationToken cancellationToken = default) => inner.ReopenAsync(issue, cancellationToken);

        public Task<GitHubCommentList> ListRecentCommentsAsync(IssueRef issue, DateTimeOffset since, CancellationToken cancellationToken = default) =>
            inner.ListRecentCommentsAsync(issue, since, cancellationToken);
    }
}
