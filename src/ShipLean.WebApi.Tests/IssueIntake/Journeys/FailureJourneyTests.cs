using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.IssueIntake;
using ShipLean.Application.Features.IssueIntake.GitHub;
using ShipLean.Application.Features.IssueIntake.Acceptance;
using ShipLean.Application.Features.IssueIntake.Inbox;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Application.Features.IssueIntake.Progress;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Webhooks;
using ShipLean.TestSupport;
using ShipLean.TestSupport.IssueIntake;
using ShipLean.TestSupport.Webhooks;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.WebApi.Tests.IssueIntake.InboxWorker;
using ShipLean.WebApi.Tests.Links.GitHub;
using ShipLean.WebApi.Tests.Webhooks;

namespace ShipLean.WebApi.Tests.IssueIntake.Journeys;

// End-to-end failure and recovery journeys for GitHub issue conversations (SHIP-742): signed deliveries through the real webhook
// endpoint, real settings saved through the API, the real propose session start and agent report path answering with the golden
// proposals, the real outbox and workers driven by hand (the loops are off in the Test profile), and a recording GitHub. The
// journeys about GitHub's HTTP answers (the ones that set _overHttp) go through the real GitHubIssuesClient against the in-process FakeGitHub
// instead; ShipLean's login always comes from the real GitHubIdentity asking FakeGitHub. Each test takes intake for
// acme/storefront with a project of its own (one enabled project per repository) and uses its own issue number.
[TestFixture]
public class FailureJourneyTests : WebhookApiTest
{
    private const string Stranger = "sam-stranger";
    private const string Instance = "https://shiplean.example.test";
    private const string Marker = "Done `<!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001 -->` here.";
    private const string Accepted = "Filter the backlog by tag";

    // WorkItemProjectLock's namespace: every work item write takes pg_advisory_xact_lock(Namespace, hashtext(project id)).
    private const int ItemLockNamespace = 0x57_49_53_4C;

    private static readonly string Goldens = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "IssueIntake", "Proposals");

    // GitHub comment ids only grow: the delivered comments' ids and FakeGitHub's come from one counter.
    private static long _commentIds = 2_470_000_000;

    private readonly FakeGitHub _github = new(FixedGitHubIdentity.DefaultLogin, () => Interlocked.Increment(ref _commentIds));

    private bool _overHttp;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [WebhookSecrets.GithubKey] = GithubSecret };

    // IGitHubIssues is scoped, as in production: the recording GitHub, or the real client once a test calls OverHttpAsync.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IHttpClientFactory>(_github);
        services.AddSingleton<IIntegrationSettings>(new FixedIntegrationSettings(new IntegrationSettingsDto("ghp_test_token", "https://api.github.test/", null, null, [])));
        services.AddSingleton<RecordingGitHubIssues>();
        services.AddScoped<IGitHubIssues>(sp => _overHttp ? sp.GetRequiredService<GitHubIssuesClient>() : sp.GetRequiredService<RecordingGitHubIssues>());
        services.AddSingleton<DbGitHubOutbox>();
        services.AddSingleton<CrashingGitHubOutbox>();
        services.AddSingleton<IGitHubOutbox>(sp => sp.GetRequiredService<CrashingGitHubOutbox>());
    };

    private RecordingGitHubIssues GitHub => Host.Services.GetRequiredService<RecordingGitHubIssues>();

    private CrashingGitHubOutbox Outbox => Host.Services.GetRequiredService<CrashingGitHubOutbox>();

    [SetUp]
    [TearDown]
    public async Task ResetAsync()
    {
        Outbox.CrashAfterEnqueue = false;
        _overHttp = false;
        _github.Reset($"/repos/{WebhookDelivery.GithubRepository}");
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
        (await NotesAsync(issue.WorkItemId)).ShouldContain(AcceptanceTexts.DuplicateOf(existing.Key));
        (await SentBodiesAsync(issue.Id))[^1].ShouldStartWith($"Thanks! This is the same as {existing.Key}: ");
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

    // GitHub's rate limit answers 403 with x-ratelimit-remaining 0: the real client makes it a retry, and the next try posts.
    [Test]
    public async Task GitHubIssue_WhenGitHubFailsThenRecovers_ShouldPostOnce()
    {
        _overHttp = true;
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(711);
        _github.PlanNextComment(711, new FakeGitHub.CommentAnswer(HttpStatusCode.Forbidden, RateLimitRemaining: "0"));

        await ProposeAsync(journey, 711, Golden("new.md"));

        var waiting = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        waiting.ShouldSatisfyAllConditions(
            () => waiting.SentAt.ShouldBeNull(), () => waiting.LastResult.ShouldBe("retry"), () => waiting.LastError.ShouldBe("403"));
        _github.Posts(711).ShouldBe(1);
        _github.Comments(711).ShouldBeEmpty();

        await MakeOutboxDueAsync(issue.Id);
        await DrainAsync();

        var sent = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        sent.ShouldSatisfyAllConditions(() => sent.SentAt.ShouldNotBeNull(), () => sent.LastResult.ShouldBe("ok"));
        _github.Posts(711).ShouldBe(2);
        _github.Comments(711).ShouldHaveSingleItem().ShouldStartWith("Thanks for the report.");
        sent.GitHubCommentId.ShouldBe(OwnPosts(711, sent).ShouldHaveSingleItem().Id);
    }

    // The POST times out after GitHub took the body: the entry is ambiguous, and the next try finds ShipLean's comment with the
    // entry's marker in the list and records it without posting again.
    [Test]
    public async Task GitHubIssue_WhenCommentPostTimesOutButLanded_ShouldNotPostTwice()
    {
        _overHttp = true;
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(712);
        _github.PlanNextComment(712, new FakeGitHub.CommentAnswer(null, Lands: true));

        await ProposeAsync(journey, 712, Golden("new.md"));

        var unsure = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        unsure.ShouldSatisfyAllConditions(
            () => unsure.SentAt.ShouldBeNull(), () => unsure.LastResult.ShouldBe("ambiguous"), () => unsure.LastError.ShouldBe("timeout"));
        _github.Comments(712).Count.ShouldBe(1);

        await MakeOutboxDueAsync(issue.Id);
        await DrainAsync();

        var row = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        row.ShouldSatisfyAllConditions(() => row.SentAt.ShouldNotBeNull(), () => row.LastResult.ShouldBe("ok"));
        row.GitHubCommentId.ShouldBe(OwnPosts(712, row).ShouldHaveSingleItem().Id);
        _github.Comments(712).Count.ShouldBe(1);
        _github.Posts(712).ShouldBe(1);
    }

    // R3: a comment carrying the entry's marker counts as landed only when ShipLean's own login wrote it (anyone can type a
    // marker). Here a stranger's copy shows up while the POST answers 502: the complete list has no ShipLean comment with the
    // marker, so the deliverer posts again.
    [Test]
    public async Task GitHubIssue_WhenMarkerListedFromAnotherLogin_ShouldPostAgain()
    {
        _overHttp = true;
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(727);
        _github.PlanNextComment(727, new FakeGitHub.CommentAnswer(HttpStatusCode.BadGateway, Plant: body => (Stranger, body)));

        await ProposeAsync(journey, 727, Golden("new.md"));
        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().LastResult.ShouldBe("ambiguous");
        await MakeOutboxDueAsync(issue.Id);
        await DrainAsync();

        var row = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        row.ShouldSatisfyAllConditions(() => row.SentAt.ShouldNotBeNull(), () => row.LastResult.ShouldBe("ok"));
        var own = OwnPosts(727, row).ShouldHaveSingleItem();
        row.GitHubCommentId.ShouldBe(own.Id);
        _github.Listing(727).ShouldContain(c => c.Login == Stranger && c.Body.Contains(IssueMarkdown.OutboxMarker(row.Id), StringComparison.Ordinal));
        _github.Posts(727).ShouldBe(2);
    }

    // R3: ShipLean's own comment counts only when it carries the entry's marker. Here a ShipLean comment without it shows up
    // while the POST answers 502: it is not this entry, so the deliverer posts again.
    [Test]
    public async Task GitHubIssue_WhenOwnCommentListedWithoutMarker_ShouldPostAgain()
    {
        _overHttp = true;
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(728);
        _github.PlanNextComment(728, new FakeGitHub.CommentAnswer(HttpStatusCode.BadGateway, Plant: body => (_github.SelfLogin, StripMarker(body))));

        await ProposeAsync(journey, 728, Golden("new.md"));
        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().LastResult.ShouldBe("ambiguous");
        await MakeOutboxDueAsync(issue.Id);
        await DrainAsync();

        var row = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        row.ShouldSatisfyAllConditions(() => row.SentAt.ShouldNotBeNull(), () => row.LastResult.ShouldBe("ok"));
        var own = OwnPosts(728, row).ShouldHaveSingleItem();
        row.GitHubCommentId.ShouldBe(own.Id);
        _github.Listing(728).Count(c => c.Login == _github.SelfLogin).ShouldBe(2);
        _github.Posts(728).ShouldBe(2);
    }

    [Test]
    public async Task GitHubIssue_WhenClosedAndReopened_ShouldPauseAndResume()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(713);
        await ProposeAsync(journey, 713, Golden("new.md"));

        await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "closed", 713, state: "closed"));
        await CommentAsync(713, "LGTM", WebhookDelivery.IssueAuthor, "NONE");
        await DrainAsync();

        var closed = await IssueAsync(713);
        closed.OpenOnGitHub.ShouldBeFalse();
        closed.Phase.ShouldBe(IssuePhase.AwaitingReply);
        closed.Rounds.ShouldBe(1);
        (await PendingJobsAsync(issue.Id)).ShouldBeEmpty();
        CommentCalls(713).ShouldBe(1);

        await DeliverAsync(AnHourLater(WebhookDelivery.GithubIssue(GithubSecret, "reopened", 713)));
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
        (await SentBodiesAsync(issue.Id))[^1].ShouldBe(ProgressMessages.Removed(await KeyAsync(issue.WorkItemId), Accepted));
    }

    // GitHub refuses the removed message with 422: the real client makes it a failure, and the close queued after it is held.
    [Test]
    public async Task GitHubIssue_WhenRemovedMessageFails_ShouldHoldClose()
    {
        _overHttp = true;
        var journey = await ArrangeAsync();
        var issue = await AcceptNewAsync(journey, 715);
        var posts = _github.Posts(715);

        await RemoveAsync(journey, issue);
        await Host.Services.GetRequiredService<ProgressReconciler>().ReconcileAsync(issue.Id, CancellationToken.None);
        _github.PlanNextComment(715, new FakeGitHub.CommentAnswer(HttpStatusCode.UnprocessableEntity));
        await DrainAsync();

        _github.Posts(715).ShouldBe(posts + 1);
        _github.Comments(715).Count.ShouldBe(posts);
        _github.Closes(715).ShouldBeEmpty();
        var rows = await OutboxAsync(issue.Id);
        var removed = rows.Last(r => r.Kind == "comment");
        removed.Body.ShouldBe(ProgressMessages.Removed(await KeyAsync(issue.WorkItemId), Accepted));
        removed.ShouldSatisfyAllConditions(
            () => removed.SentAt.ShouldBeNull(), () => removed.FailedAt.ShouldNotBeNull(), () => removed.LastResult.ShouldBe("failed"), () => removed.LastError.ShouldBe("422"));
        var close = rows.Where(r => r.Kind == "close").ShouldHaveSingleItem();
        close.ShouldSatisfyAllConditions(() => close.SentAt.ShouldBeNull(), () => close.FailedAt.ShouldNotBeNull(), () => close.LastResult.ShouldBe("failed"));
        (await NotesAsync(issue.WorkItemId)).ShouldContain(n => n.Contains("(422)", StringComparison.Ordinal) && n.Contains("close", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task GitHubIssue_WhenBotComments_ShouldIgnore()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(716);
        await ProposeAsync(journey, 716, Golden("new.md"));

        var bot = await CommentAsync(716, "LGTM", "helper-app[bot]", "MEMBER", authorType: "Bot");
        var self = await CommentAsync(716, "LGTM", _github.SelfLogin, "MEMBER");
        // The control: an ordinary stranger's comment on the same issue is mirrored.
        var stranger = await CommentAsync(716, "Same problem here.", Stranger, "NONE");
        await DrainAsync();

        bot.Outcome.ShouldBe(WebhookOutcome.Ignored);
        self.Outcome.ShouldBe(WebhookOutcome.Ignored);
        stranger.Outcome.ShouldBe(WebhookOutcome.Processed);
        (await IssueAsync(716)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await MirroredAsync(issue.Id)).ShouldBe([$"{Stranger}: Same problem here."]);
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

        // The early LGTM accepted nothing: still proposing, no accept job ran, and the Triage item is the project's only item.
        var busy = await IssueAsync(717);
        busy.Phase.ShouldBe(IssuePhase.Proposing);
        busy.WorkItemId.ShouldBe(issue.WorkItemId);
        await using (var database = await OpenDatabaseAsync())
        {
            (await database.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM issue_job WHERE github_issue_id = @Id AND kind = 'accept' AND status = 'done'", new { issue.Id })).ShouldBe(0);
            (await database.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @ProjectId", new { issue.ProjectId })).ShouldBe(1);
        }

        (await StateNameAsync(issue.WorkItemId)).ShouldBe("Triage");

        await RespondAsync(journey, 717, Golden("new.md").Replace("by tag", "by label", StringComparison.Ordinal));
        await DrainAsync();
        await CommentAsync(717, "LGTM", WebhookDelivery.IssueAuthor, "NONE");
        await DrainAsync();

        (await IssueAsync(717)).Phase.ShouldBe(IssuePhase.Accepted);
        (await TitleAsync(issue.WorkItemId)).ShouldBe("Filter the backlog by label");
    }

    // R4: neither delivery is recorded yet, so both pass the delivery dedupe and reach the Triage item's create, which takes the
    // project's work item lock. Held there until both wait, they overlap: the second finds the first's github_issue row and
    // rolls its own item back.
    [Test]
    public async Task GitHubIssue_WhenSameDeliveryTwiceAtOnce_ShouldActOnce()
    {
        var journey = await ArrangeAsync();
        var opened = WebhookDelivery.GithubIssue(GithubSecret, "opened", 718);

        var (first, second) = await RaceAtLockAsync(ItemLock(journey), () => DeliverAsync(opened), () => DeliverAsync(opened));

        first.Outcome.ShouldBe(WebhookOutcome.Processed);
        second.Outcome.ShouldBe(WebhookOutcome.Processed);
        var issue = await IssueAsync(718);
        var key = await KeyAsync(issue.WorkItemId);
        first.Item.ShouldBe(key);
        second.Item.ShouldBe(key);
        await using var database = await OpenDatabaseAsync();
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM github_issue WHERE number = 718")).ShouldBe(1);
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM work_item WHERE project_id = @ProjectId", new { issue.ProjectId })).ShouldBe(1);
        (await PendingJobsAsync(issue.Id)).ShouldBe(["round"]);
    }

    [Test]
    public async Task GitHubIssue_WhenSameReplyTwiceAtOnce_ShouldAskOneRound()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(719);
        await ProposeAsync(journey, 719, Golden("new.md"));
        var reply = WebhookDelivery.GithubIssueComment(
            GithubSecret, "Tags on Features too, please.", WebhookDelivery.IssueAuthor, "NONE", 719, commentId: Interlocked.Increment(ref _commentIds));

        // R4: a reply takes the project's round lock first; held there until both wait, the two deliveries overlap.
        var (first, second) = await RaceAtLockAsync(RoundLock(journey), () => DeliverAsync(reply), () => DeliverAsync(reply));

        first.Outcome.ShouldBe(WebhookOutcome.Processed);
        second.Outcome.ShouldBe(WebhookOutcome.Processed);
        (await PendingJobsAsync(issue.Id)).ShouldBe(["round"]);
        (await MirroredAsync(issue.Id)).ShouldHaveSingleItem();
        await DrainAsync();
        (await IssueAsync(719)).Rounds.ShouldBe(2);
    }

    [Test]
    public async Task GitHubIssue_WhenReplyDuringProposal_ShouldRunAnotherRoundAfterIt()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(720);
        await DrainAsync();
        var first = (await IssueAsync(720)).ProposalSessionId;

        await CommentAsync(720, "It should work on mobile too.", WebhookDelivery.IssueAuthor, "NONE");
        await RespondAsync(journey, 720, Golden("new.md"));
        await DrainAsync();

        var next = await IssueAsync(720);
        next.Phase.ShouldBe(IssuePhase.Proposing);
        next.Rounds.ShouldBe(2);
        next.ProposalSessionId.ShouldNotBe(first);
        (await SentBodiesAsync(issue.Id)).ShouldHaveSingleItem().ShouldStartWith("Thanks for the report.");
    }

    [Test]
    public async Task GitHubIssue_WhenAcceptFailsForGood_ShouldReleaseAndAcceptRetry()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(721);
        await ProposeAsync(journey, 721, Golden("new.md"));
        await CommentAsync(721, "LGTM", WebhookDelivery.IssueAuthor, "NONE");
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync(
                "UPDATE issue_job SET attempts = @last WHERE github_issue_id = @Id AND kind = 'accept'", new { last = IIssueJobs.MaxAttempts - 1, issue.Id });
        }

        Outbox.CrashAfterEnqueue = true;
        await DrainAsync();
        Outbox.CrashAfterEnqueue = false;

        (await IssueAsync(721)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await NotesAsync(issue.WorkItemId)).ShouldContain(AcceptanceTexts.Failed);
        (await SentBodiesAsync(issue.Id)).ShouldHaveSingleItem();

        await CommentAsync(721, "LGTM", WebhookDelivery.Maintainer, "MEMBER");
        await DrainAsync();

        (await IssueAsync(721)).Phase.ShouldBe(IssuePhase.Accepted);
        (await StateNameAsync(issue.WorkItemId)).ShouldBe("New");
        (await SentBodiesAsync(issue.Id)).Count.ShouldBe(2);
    }

    [Test]
    public async Task GitHubIssue_WhenEachWorkerCrashesAfterEnqueue_ShouldResumeWithoutDuplicates()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(722);
        await DrainAsync();

        // The proposal worker dies after queueing the proposal; its retry posts it once.
        await RespondAsync(journey, 722, Golden("new.md"));
        await CrashThenResumeAsync(issue.Id);
        (await IssueAsync(722)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await SentBodiesAsync(issue.Id)).Count.ShouldBe(1);

        // The accept worker dies after queueing its reply.
        await CommentAsync(722, "LGTM", WebhookDelivery.IssueAuthor, "NONE");
        await CrashThenResumeAsync(issue.Id);
        (await IssueAsync(722)).Phase.ShouldBe(IssuePhase.Accepted);
        (await SentBodiesAsync(issue.Id)).Count.ShouldBe(2);

        // The progress reconciler dies after queueing the removed message.
        await RemoveAsync(journey, await IssueAsync(722));
        var reconciler = Host.Services.GetRequiredService<ProgressReconciler>();
        Outbox.CrashAfterEnqueue = true;
        await reconciler.RunPassAsync(CancellationToken.None);
        Outbox.CrashAfterEnqueue = false;
        (await OutboxAsync(issue.Id)).Count.ShouldBe(2);
        await reconciler.RunPassAsync(CancellationToken.None);
        await DrainAsync();

        var calls = GitHubCalls(722);
        calls.Count(c => c.StartsWith("comment ", StringComparison.Ordinal)).ShouldBe(3);
        calls.Count(c => c.StartsWith("close ", StringComparison.Ordinal)).ShouldBe(1);
        calls[^1].ShouldBe($"close {Ref(722)} notPlanned");
    }

    [Test]
    public async Task GitHubIssue_WhenRoundWorkerCrashesHolding_ShouldHoldOnce()
    {
        var journey = await ArrangeAsync();
        await SetConnectionAsync(journey, null);
        var issue = await OpenAsync(723);

        await CrashThenResumeAsync(issue.Id);

        (await IssueAsync(723)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        (await SentBodiesAsync(issue.Id)).ShouldBe([RoundHolds.Comment]);
        (await NotesAsync(issue.WorkItemId)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task GitHubIssue_WhenMemberCreatesItemAsIntakeDoes_ShouldCreateBoth()
    {
        var journey = await ArrangeAsync();

        // R4: both creates take the project's work item lock; held there until both wait, they overlap.
        var (response, opened) = await RaceAtLockAsync(
            ItemLock(journey),
            () => Api.PostJsonAsync("api/work-items", new { projectId = journey.Arranged.Project.Id, type = "story", title = "A member's story", parentId = (Guid?)null, sprintId = (Guid?)null }),
            () => DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "opened", 724)));

        using (response)
        {
            response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        }

        opened.Outcome.ShouldBe(WebhookOutcome.Processed);
        await using var database = await OpenDatabaseAsync();
        var keys = (await database.QueryAsync<int>("SELECT number FROM work_item WHERE project_id = @Id", new { journey.Arranged.Project.Id })).ToList();
        keys.Count.ShouldBe(2);
        keys.Distinct().Count().ShouldBe(2);
        (await StateNameAsync((await IssueAsync(724)).WorkItemId)).ShouldBe("Triage");
    }

    // D13 (SHIP-921): a round ends in a post and nothing posts while the issue is closed on GitHub, so the author's reply on a
    // closed issue is only mirrored.
    [Test]
    public async Task GitHubIssue_WhenAuthorRepliesWhileClosed_ShouldOnlyMirror()
    {
        var journey = await ArrangeAsync();
        var issue = await OpenAsync(725);
        await ProposeAsync(journey, 725, Golden("new.md"));
        await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "closed", 725, state: "closed"));
        await DrainAsync();
        var before = await IssueAsync(725);
        var outboxBefore = (await OutboxAsync(issue.Id)).Count;
        var roundsBefore = await RoundJobCountAsync(issue.Id);

        (await CommentAsync(725, "Please also cover Features.", WebhookDelivery.IssueAuthor, "NONE")).Outcome.ShouldBe(WebhookOutcome.Processed);
        await DrainAsync();

        var after = await IssueAsync(725);
        after.OpenOnGitHub.ShouldBeFalse();
        after.Phase.ShouldBe(before.Phase);
        after.Rounds.ShouldBe(before.Rounds);
        (await RoundJobCountAsync(issue.Id)).ShouldBe(roundsBefore);
        (await PendingJobsAsync(issue.Id)).ShouldBeEmpty();
        (await OutboxAsync(issue.Id)).Count.ShouldBe(outboxBefore);
        (await MirroredAsync(issue.Id)).ShouldBe([$"{WebhookDelivery.IssueAuthor}: Please also cover Features."]);
    }

    // A maintainer's /shiplean on a held issue that is closed on GitHub does nothing until the issue is reopened.
    [Test]
    public async Task GitHubIssue_WhenSlashShipleanWhileClosed_ShouldStayHeld()
    {
        var journey = await ArrangeAsync();
        await SetConnectionAsync(journey, null);
        var issue = await OpenAsync(726);
        await DrainAsync();
        await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "closed", 726, state: "closed"));
        await SetConnectionAsync(journey, journey.Arranged.ConnectionId);
        var roundsBefore = await RoundJobCountAsync(issue.Id);
        var outboxBefore = (await OutboxAsync(issue.Id)).Count;

        await CommentAsync(726, "/shiplean", WebhookDelivery.Maintainer, "MEMBER");
        await DrainAsync();

        var after = await IssueAsync(726);
        after.Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        after.Rounds.ShouldBe(1);
        after.ProposalSessionId.ShouldBeNull();
        (await RoundJobCountAsync(issue.Id)).ShouldBe(roundsBefore);
        (await OutboxAsync(issue.Id)).Count.ShouldBe(outboxBefore);
    }

    // WorkItemProjectLock's key for the journey's project.
    private static HeldLock ItemLock(Journey journey) =>
        new("SELECT pg_advisory_xact_lock(@ns, hashtext(@projectId::text))", new { ns = ItemLockNamespace, projectId = journey.Arranged.Project.Id });

    // IIssueProposalSessions.LockProjectAsync's key for the journey's project.
    private static HeldLock RoundLock(Journey journey) =>
        new("SELECT pg_advisory_xact_lock(hashtextextended('issue_round:' || @projectId::text, 0))", new { projectId = journey.Arranged.Project.Id });

    // Takes the lock in a transaction of its own, starts both requests, polls pg_locks until both wait on that lock (two requests
    // for the same advisory key that are not granted), then commits to release it, so both are inside the section it guards at once.
    // A request that finishes without waiting, or a poll that runs out (500 polls of 20 ms), fails the test.
    private async Task<(T1 First, T2 Second)> RaceAtLockAsync<T1, T2>(HeldLock held, Func<Task<T1>> first, Func<Task<T2>> second)
    {
        const string Waiting =
            """
            SELECT count(*)::int
            FROM pg_sleep(0.02) AS pause, pg_locks h
            JOIN pg_locks w ON w.locktype = 'advisory' AND NOT w.granted AND w.database = h.database
             AND w.classid = h.classid AND w.objid = h.objid AND w.objsubid = h.objsubid
            WHERE h.locktype = 'advisory' AND h.granted AND h.pid = pg_backend_pid()
            """;
        await using var database = await OpenDatabaseAsync();
        await using var transaction = await database.BeginTransactionAsync();
        await database.ExecuteAsync(held.Sql, held.Args, transaction);
        var one = first();
        var two = second();
        var waiting = 0;
        for (var poll = 0; poll < 500 && waiting < 2 && !one.IsCompleted && !two.IsCompleted; poll++)
        {
            waiting = await database.ExecuteScalarAsync<int>(Waiting, transaction: transaction);
        }

        await transaction.CommitAsync();
        var results = (await one, await two);
        waiting.ShouldBe(2, "both requests wait at the held lock before it is released");
        return results;
    }

    // Every round job of the issue, done or not.
    private async Task<int> RoundJobCountAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<int>("SELECT count(*)::int FROM issue_job WHERE github_issue_id = @issueId AND kind = 'round'", new { issueId });
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

    // Runs every worker once with the outbox crashing after each enqueue (the job fails and backs off), then makes the jobs due
    // again and runs them normally.
    private async Task CrashThenResumeAsync(Guid issueId)
    {
        Outbox.CrashAfterEnqueue = true;
        await DrainAsync();
        Outbox.CrashAfterEnqueue = false;
        await using (var database = await OpenDatabaseAsync())
        {
            (await database.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM issue_job WHERE github_issue_id = @issueId AND status = 'pending' AND last_error IS NOT NULL", new { issueId })).ShouldBe(1);
            await database.ExecuteAsync(
                "UPDATE issue_job SET next_attempt_at = now(), leased_until = NULL WHERE github_issue_id = @issueId AND status = 'pending'", new { issueId });
        }

        await DrainAsync();
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

    // The recorded payload carries one updated_at, and an older or equal event doesn't move the issue's open flag: a reopen comes
    // later on GitHub, so its updated_at is read, moved an hour on, written back in GitHub's format and the body signed again.
    private static WebhookDelivery AnHourLater(WebhookDelivery delivery)
    {
        var payload = JsonNode.Parse(delivery.Body)!;
        var issue = payload["issue"]!;
        var later = issue["updated_at"]!.GetValue<DateTimeOffset>().AddHours(1);
        issue["updated_at"] = later.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var body = payload.ToJsonString();
        var signature = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(GithubSecret), Encoding.UTF8.GetBytes(body)));
        return (delivery with { Body = body }).WithHeader("X-Hub-Signature-256", signature);
    }

    private static IssueRef Ref(int number) => new(IssueRef.GitHubHost, WebhookDelivery.GithubRepository, number);

    private static string Golden(string name) => File.ReadAllText(Path.Combine(Goldens, name));

    private IReadOnlyList<string> GitHubCalls(int number) =>
        [.. GitHub.Calls.Where(c => c.EndsWith($"#{number}", StringComparison.Ordinal) || c.Contains($"#{number} ", StringComparison.Ordinal))];

    private int CommentCalls(int number) => GitHubCalls(number).Count(c => c.StartsWith("comment ", StringComparison.Ordinal));

    // The comments on FakeGitHub's issue by ShipLean's login that carry the entry's marker.
    private List<FakeGitHub.Listed> OwnPosts(int number, OutboxRow entry) =>
        [.. _github.Listing(number).Where(c => c.Login == _github.SelfLogin && c.Body.Contains(IssueMarkdown.OutboxMarker(entry.Id), StringComparison.Ordinal))];

    private async Task<List<OutboxRow>> OutboxAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<OutboxRow>(
            """
            SELECT id, kind, dedupe_key AS dedupeKey, body, sent_at AS sentAt, failed_at AS failedAt, github_comment_id AS gitHubCommentId,
                   last_result AS lastResult, last_error AS lastError
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

    private async Task<string> KeyAsync(Guid workItemId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @workItemId", new { workItemId });
    }

    private async Task<string> TitleAsync(Guid workItemId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<string>("SELECT title FROM work_item WHERE id = @workItemId", new { workItemId });
    }

    private sealed record Journey(AgentDispatchArrange Arranged);

    private sealed record HeldLock(string Sql, object Args);

    private sealed record OutboxRow(
        Guid Id, string Kind, string DedupeKey, string? Body, DateTimeOffset? SentAt, DateTimeOffset? FailedAt, long? GitHubCommentId, string? LastResult, string? LastError);
}
