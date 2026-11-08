using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Application.Features.IssueIntake.Inbox;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Application.Events;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Application.Features.IssueIntake.ShippedIn;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.IssueIntake;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.IssueIntake.InboxWorker;

// The proposal job worker (C2, D11, D17, D18): a propose session's response is parsed, guarded, saved and posted once, in one
// transaction with the phase change and the job's completion. Other fixtures leave proposal jobs behind, so each test drains every
// due job, and the teardown completes whatever is left.
[TestFixture]
public class ProposalJobTests : ApiFixture
{
    private const string Valid = """{ "verdict": "new", "type": "story", "title": "Fix the login label", "description": "The button says Sing in." }""";

    private const string Instance = "https://shiplean.example.test";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<FixedIssueIntakeSettings>();
        services.AddScoped<IIssueIntakeSettings>(sp => sp.GetRequiredService<FixedIssueIntakeSettings>());
        services.AddSingleton<DbGitHubOutbox>();
        services.AddSingleton<CrashingGitHubOutbox>();
        services.AddSingleton<IGitHubOutbox>(sp => sp.GetRequiredService<CrashingGitHubOutbox>());
        services.AddSingleton<FixedImagePublications>();
        services.AddSingleton<IImagePublications>(sp => sp.GetRequiredService<FixedImagePublications>());
        services.AddSingleton<InMemoryProjectEvents>();
        services.AddSingleton<CommitCheckingProjectEvents>();
        services.AddSingleton<IProjectEvents>(sp => sp.GetRequiredService<CommitCheckingProjectEvents>());
    };

    private AgentDispatchArrange? _arranged;

    private ProposalJobWorker Worker => Host.Services.GetRequiredService<ProposalJobWorker>();

    private IGitHubIssueStore Issues => Host.Services.GetRequiredService<IGitHubIssueStore>();

    [SetUp]
    [TearDown]
    public async Task CompleteLeftoversAsync()
    {
        var outbox = Host.Services.GetRequiredService<CrashingGitHubOutbox>();
        outbox.CrashAfterEnqueue = false;
        outbox.AfterEnqueue = null;
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE status = 'pending' AND kind = 'proposal'");
        await database.ExecuteAsync("UPDATE instance_settings SET base_url = @Instance", new { Instance });
    }

    [Test]
    public async Task Proposal_WhenValid_ShouldEnqueueGuardedCommentOnce()
    {
        var (issue, sessionId) = await ArrangeAsync();
        var name = await RunAsNameAsync();
        await EnqueueAsync(issue, sessionId, Response($"Thanks! {name} can confirm at joel@example.com.", Valid));

        await DrainAsync();

        var posted = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        posted.DedupeKey.ShouldBe(ProposalJobWorker.DedupeKey(sessionId));
        posted.Body.ShouldNotBeNull().ShouldEndWith($"\n\n{ProposalJobWorker.Footer}");
        posted.Body.ShouldStartWith("Thanks!");
        posted.Body.ShouldNotContain(name);
        posted.Body.ShouldNotContain("joel@example.com");
        var stored = await IssueAsync(issue.Id);
        stored.Phase.ShouldBe(IssuePhase.AwaitingReply);
        stored.ProposalSessionId.ShouldBe(sessionId);
        JsonDocument.Parse(stored.Proposal!).RootElement.GetProperty("title").GetString().ShouldBe("Fix the login label");
        (await JobStatusesAsync(issue.Id)).ShouldBe(["done"]);
    }

    [Test]
    public async Task Proposal_WhenReprocessed_ShouldNotPostTwice()
    {
        var (issue, sessionId) = await ArrangeAsync();
        var response = Response("A fix is proposed.", Valid);
        await EnqueueAsync(issue, sessionId, response);
        await DrainAsync();

        await EnqueueAsync(issue, sessionId, Response("Another fix.", Valid.Replace("Fix the login label", "Another title", StringComparison.Ordinal)), key: $"proposal:{sessionId}:again");
        await DrainAsync();

        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().Body.ShouldNotBeNull().ShouldStartWith("A fix is proposed.");
        var stored = await IssueAsync(issue.Id);
        stored.Phase.ShouldBe(IssuePhase.AwaitingReply);
        JsonDocument.Parse(stored.Proposal!).RootElement.GetProperty("title").GetString().ShouldBe("Fix the login label");
        (await JobStatusesAsync(issue.Id)).ShouldBe(["done", "done"]);
    }

    [Test]
    public async Task Proposal_WhenAlreadyShipped_ShouldSetPhase()
    {
        var (issue, sessionId) = await ArrangeAsync();
        await EnqueueAsync(issue, sessionId, Response(
            "This already shipped.", """{ "verdict": "alreadyShipped", "type": "story", "title": "Login label", "existing": [{ "key": "SHIP-1" }] }"""));

        await DrainAsync();

        (await IssueAsync(issue.Id)).Phase.ShouldBe(IssuePhase.AlreadyShipped);
        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().Body.ShouldBe(
            $"This already shipped.\n\n{ShippedInSection.Heading}\n\n{ShippedInSection.Unconfirmed}\n\n{ProposalJobWorker.Footer}");
    }

    [Test]
    public async Task Proposal_WhenAlreadyShippedVerified_ShouldRenderShippedInFacts()
    {
        var (issue, sessionId) = await ArrangeAsync();
        var shipped = await AddMergedItemAsync();
        var clock = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        var publishedAt = clock.AddTicks(-(clock.Ticks % TimeSpan.TicksPerMinute));
        var runUrl = "https://github.com/shiplean-tests/inbox/actions/runs/42";
        Host.Services.GetRequiredService<FixedImagePublications>().Set(shipped.Sha, new ImagePublication.Published(publishedAt, runUrl, shipped.Sha));
        await EnqueueAsync(issue, sessionId, Response("This already shipped.", ShippedJson(
            $$"""{ "key": "{{shipped.Key}}", "commit": "abcdef1234", "publishedAt": "2001-01-01T00:00:00Z", "runUrl": "https://github.com/agent/claimed/actions/runs/7" }""",
            "\"The webhook secret is set\"")));

        await DrainAsync();

        var body = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().Body.ShouldNotBeNull();
        var published = publishedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        body.ShouldStartWith($"This already shipped.\n\n{ShippedInSection.Heading}\n\n- {shipped.Key}: commit `{shipped.Sha[..7]}` (shiplean-tests/inbox#{shipped.Pull}), merged ");
        body.ShouldContain($"Image published {published} (run: {runUrl}).");
        body.ShouldContain("`<your instance>/health`; it shows `version: 1.0.0+<sha>`");
        body.ShouldContain($"`git merge-base --is-ancestor {shipped.Sha[..7]} <sha>`");
        body.ShouldContain($"your `:latest` image was pulled after {published}");
        body.ShouldContain("It works only when:\n- The webhook secret is set");
        body.ShouldNotContain("abcdef1");
        body.ShouldNotContain("2001-01-01");
        body.ShouldNotContain("agent/claimed");
        body.ShouldNotContain(ShippedInSection.Unconfirmed);
    }

    [Test]
    public async Task Proposal_WhenShippedInUnverified_ShouldNotRenderAgentClaims()
    {
        var (issue, sessionId) = await ArrangeAsync();
        var open = await AddMergedItemAsync(state: "open");
        Host.Services.GetRequiredService<FixedImagePublications>().Set(open.Sha, new ImagePublication.Published(Host.Services.GetRequiredService<TimeProvider>().GetUtcNow(), "https://github.com/x/y/actions/runs/1", open.Sha));
        await EnqueueAsync(issue, sessionId, Response("This already shipped.", ShippedJson(
            $$"""{ "key": "{{open.Key}}", "commit": "{{open.Sha}}", "prUrl": "https://github.com/shiplean-tests/inbox/pull/{{open.Pull}}", "publishedAt": "2001-01-01T00:00:00Z", "runUrl": "https://github.com/agent/claimed/actions/runs/7" }, { "key": "ZZZ-404", "commit": "abcdef1234" }""",
            "")));

        await DrainAsync();

        var body = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().Body.ShouldNotBeNull();
        body.ShouldBe($"This already shipped.\n\n{ShippedInSection.Heading}\n\n{ShippedInSection.Unconfirmed}\n\n{ProposalJobWorker.Footer}");
    }

    [Test]
    public async Task Proposal_WhenShippedInValuesMalicious_ShouldGuardThem()
    {
        var (issue, sessionId) = await ArrangeAsync();
        var name = await RunAsNameAsync();
        await EnqueueAsync(issue, sessionId, Response("This already shipped.", ShippedJson(
            $$"""{ "prUrl": "{{Instance}}/p/SHIP/items/SHIP-9", "runUrl": "{{Instance}}/admin", "commit": "abcdef1234" }""",
            $$"""
            "Ask {{name}} at joel@example.com", "See {{Instance}}/p/SHIP/settings", "@maintainer-team must approve", "line one\n# heading"
            """)));

        await DrainAsync();

        var body = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().Body.ShouldNotBeNull();
        body.ShouldContain(ShippedInSection.Unconfirmed);
        body.ShouldContain("It works only when:");
        body.ShouldNotContain(name);
        body.ShouldNotContain("joel@example.com");
        body.ShouldNotContain("shiplean.example.test");
        body.ShouldNotContain(" @maintainer-team");
        body.ShouldNotContain("abcdef1");
        body.ShouldNotContain("\n# heading");
    }

    [Test]
    public async Task Proposal_WhenBaseUrlBlank_ShouldHoldWithoutPosting()
    {
        var (issue, sessionId) = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE instance_settings SET base_url = '  '");
        }

        await EnqueueAsync(issue, sessionId, Response("A fix is proposed.", Valid));

        await DrainAsync();

        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
        var stored = await IssueAsync(issue.Id);
        stored.Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        stored.Proposal.ShouldNotBeNull();
        (await ShipLeanCommentsAsync(issue.WorkItemId)).ShouldHaveSingleItem().Body.ShouldBe($"{ProposalJobWorker.NoBaseUrlNote} {IssueProposalSessions.Continue}");
        (await JobStatusesAsync(issue.Id)).ShouldBe(["done"]);
    }

    [Test]
    public async Task Proposal_WhenOlderRoundArrivesLate_ShouldWriteNothing()
    {
        var (issue, olderSession) = await ArrangeAsync();
        var newerSession = Guid.CreateVersion7();
        await StartedSessionAsync(issue, newerSession);

        await EnqueueAsync(issue, olderSession, Response("An old proposal.", Valid));
        await EnqueueAsync(issue, olderSession, "Unreadable old response.", key: $"proposal:{olderSession}:unreadable");

        await DrainAsync();

        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
        var stored = await IssueAsync(issue.Id);
        stored.Phase.ShouldBe(IssuePhase.Proposing);
        stored.ProposalSessionId.ShouldBe(newerSession);
        stored.Proposal.ShouldBeNull();
        (await ShipLeanCommentsAsync(issue.WorkItemId)).ShouldBeEmpty();
        (await JobStatusesAsync(issue.Id)).ShouldBe(["done", "done"]);
    }

    [Test]
    public async Task Proposal_WhenClosedThenReopened_ShouldPostOnce()
    {
        var (issue, sessionId) = await ArrangeAsync();
        var at = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        await ChangedAsync(issue, IssueAction.Closed, at);
        await EnqueueAsync(issue, sessionId, Response("A fix is proposed.", Valid));
        await DrainAsync();
        var stored = await IssueAsync(issue.Id);
        stored.Phase.ShouldBe(IssuePhase.AwaitingReply);
        stored.Proposal.ShouldNotBeNull();
        var original = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        await SkipClosedAsync(issue.Id);

        await ChangedAsync(issue, IssueAction.Reopened, at.AddMinutes(1));
        await ChangedAsync(issue, IssueAction.Reopened, at.AddMinutes(2));

        var deliverable = (await OutboxAsync(issue.Id)).Where(o => !o.Sent).ToList();
        var repost = deliverable.ShouldHaveSingleItem();
        repost.DedupeKey.ShouldBe($"{ProposalJobWorker.DedupeKey(sessionId)}:reopen");
        repost.Body.ShouldBe(original.Body);
    }

    [Test]
    public async Task Proposal_WhenReopenedBeforeDelivery_ShouldNotEnqueueAgain()
    {
        var (issue, sessionId) = await ArrangeAsync();
        var at = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        await ChangedAsync(issue, IssueAction.Closed, at);
        await EnqueueAsync(issue, sessionId, Response("A fix is proposed.", Valid));
        await DrainAsync();

        await ChangedAsync(issue, IssueAction.Reopened, at.AddMinutes(1));

        var pending = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        pending.DedupeKey.ShouldBe(ProposalJobWorker.DedupeKey(sessionId));
        pending.Sent.ShouldBeFalse();
    }

    [Test]
    public async Task Proposal_WhenLeaseGoesStale_ShouldRollBackEveryWrite()
    {
        var (issue, sessionId) = await ArrangeAsync();
        await Issues.RequestRerunAsync(issue.Id);
        await EnqueueAsync(issue, sessionId, Response("A fix is proposed.", Valid));
        Host.Services.GetRequiredService<CrashingGitHubOutbox>().AfterEnqueue = async () =>
        {
            // Another worker claims the job meanwhile: a new lease token, on a separate connection.
            await using var other = await OpenDatabaseAsync();
            await other.ExecuteAsync("UPDATE issue_job SET lease_token = gen_random_uuid() WHERE github_issue_id = @Id AND kind = 'proposal'", new { issue.Id });
        };

        await DrainAsync();

        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
        var stored = await IssueAsync(issue.Id);
        stored.Phase.ShouldBe(IssuePhase.Proposing);
        stored.Proposal.ShouldBeNull();
        stored.Rounds.ShouldBe(0);
        (await Issues.TakeRerunAsync(issue.Id)).ShouldBeTrue();
        (await JobStatusesAsync(issue.Id)).ShouldBe(["pending"]);
        await using var database = await OpenDatabaseAsync();
        (await database.ExecuteScalarAsync<int>("SELECT count(*)::int FROM issue_job WHERE github_issue_id = @Id AND kind = 'round'", new { issue.Id })).ShouldBe(0);
    }

    [Test]
    public async Task Proposal_WhenRerunHeld_ShouldPublishChangedAfterCommit()
    {
        var (issue, sessionId) = await ArrangeAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE github_issue SET rounds = 5 WHERE id = @Id", new { issue.Id });
        }

        await Issues.RequestRerunAsync(issue.Id);
        await EnqueueAsync(issue, sessionId, Response("A fix is proposed.", Valid));

        await DrainAsync();

        (await IssueAsync(issue.Id)).Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        var published = Host.Services.GetRequiredService<CommitCheckingProjectEvents>().Published.Where(p => p.WorkItemId == issue.WorkItemId).ToList();
        published.ShouldHaveSingleItem().Committed.ShouldBe(1);
    }

    [Test]
    public async Task Proposal_WhenUnparseable_ShouldNotPostToGitHub()
    {
        var (issue, sessionId) = await ArrangeAsync();
        await EnqueueAsync(issue, sessionId, "I could not decide.");

        await DrainAsync();

        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
        var stored = await IssueAsync(issue.Id);
        stored.Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        stored.Proposal.ShouldBeNull();
        var note = (await ShipLeanCommentsAsync(issue.WorkItemId)).ShouldHaveSingleItem();
        note.AuthorId.ShouldBe(SeedIds.Users.Joel);
        note.Body.ShouldBe($"Proposal could not be read: The response has no shiplean-proposal block. {IssueProposalSessions.Continue}");
        (await JobStatusesAsync(issue.Id)).ShouldBe(["done"]);
    }

    [Test]
    public async Task Proposal_WhenGuardRefuses_ShouldHoldWithoutPostingIt()
    {
        var (issue, sessionId) = await ArrangeAsync();
        await EnqueueAsync(issue, sessionId, Response("Done `<!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001 -->` here.", Valid));

        await DrainAsync();

        var held = (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        held.Body.ShouldBe(RoundHolds.Comment);
        held.DedupeKey.ShouldStartWith($"hold:{issue.Id}:{ProposalJobWorker.GuardBlocked}:");
        var stored = await IssueAsync(issue.Id);
        stored.Phase.ShouldBe(IssuePhase.WaitingForMaintainer);
        stored.ProposalSessionId.ShouldBe(sessionId);
        (await ShipLeanCommentsAsync(issue.WorkItemId)).ShouldHaveSingleItem().Body.ShouldStartWith(
            "Held for a maintainer: the proposal was not posted on GitHub because it failed the public text check (unsafe: ");
    }

    [Test]
    public async Task Proposal_WhenRerunRequested_ShouldRequestRound()
    {
        var (issue, sessionId) = await ArrangeAsync();
        await Issues.RequestRerunAsync(issue.Id);
        await EnqueueAsync(issue, sessionId, Response("A fix is proposed.", Valid));

        await DrainAsync();

        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().DedupeKey.ShouldBe(ProposalJobWorker.DedupeKey(sessionId));
        var stored = await IssueAsync(issue.Id);
        stored.Phase.ShouldBe(IssuePhase.Proposing);
        stored.Rounds.ShouldBe(1);
        (await Issues.TakeRerunAsync(issue.Id)).ShouldBeFalse();
        await using var database = await OpenDatabaseAsync();
        (await database.QueryAsync<string>(
            "SELECT key FROM issue_job WHERE github_issue_id = @Id AND kind = 'round' AND status = 'pending'", new { issue.Id }))
            .ShouldHaveSingleItem().ShouldBe(IssueProposalSessions.JobKey(1, issue.Id));
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE github_issue_id = @Id AND status = 'pending'", new { issue.Id });
    }

    [Test]
    public async Task Proposal_WhenWorkerCrashes_ShouldResume()
    {
        var (issue, sessionId) = await ArrangeAsync();
        await EnqueueAsync(issue, sessionId, Response("A fix is proposed.", Valid));
        var outbox = Host.Services.GetRequiredService<CrashingGitHubOutbox>();
        outbox.CrashAfterEnqueue = true;

        await DrainAsync();

        (await OutboxAsync(issue.Id)).ShouldBeEmpty();
        (await IssueAsync(issue.Id)).Phase.ShouldBe(IssuePhase.Proposing);
        (await JobStatusesAsync(issue.Id)).ShouldBe(["pending"]);

        outbox.CrashAfterEnqueue = false;
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE issue_job SET next_attempt_at = created_at WHERE github_issue_id = @Id", new { issue.Id });
        }

        await DrainAsync();

        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().DedupeKey.ShouldBe(ProposalJobWorker.DedupeKey(sessionId));
        (await IssueAsync(issue.Id)).Phase.ShouldBe(IssuePhase.AwaitingReply);
        (await JobStatusesAsync(issue.Id)).ShouldBe(["done"]);
    }

    private static string Response(string markdown, string json) => $"{markdown}\n\n```shiplean-proposal\n{json}\n```\n";

    private async Task<(GitHubIssueRecord Issue, Guid SessionId)> ArrangeAsync()
    {
        AgentDispatchArrange arranged;
        await using (var database = await OpenDatabaseAsync())
        {
            arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        }

        _arranged = arranged;

        var item = (await arranged.CreateItemAsync(Api)).Id;
        var repository = $"shiplean-tests/inbox-{Guid.NewGuid():N}";
        var issue = await Issues.InsertAsync(
            new IssueRef(IssueRef.GitHubHost, repository, 1), arranged.Project.Id, item, "reporter", "Login button says Sing in", "The issue body.", IssuePhase.Proposing);
        var sessionId = Guid.CreateVersion7();
        await using (var database = await OpenDatabaseAsync())
        {
            var key = await database.QuerySingleAsync<string>("SELECT key FROM project WHERE id = @id", new { id = arranged.Project.Id });
            Host.Services.GetRequiredService<FixedIssueIntakeSettings>()
                .Take(new IssueIntakeProject(arranged.Project.Id, key, repository, SeedIds.Users.Joel, "Instructions.", 5));
        }

        await StartedSessionAsync(issue, sessionId);
        return (await IssueAsync(issue.Id), sessionId);
    }

    // A finished propose session on the issue's item, made the issue's proposal session the way the round worker's fence does.
    private async Task StartedSessionAsync(GitHubIssueRecord issue, Guid sessionId)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            """
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, created_by, created_at, mode, ended_at)
            VALUES (@sessionId, @WorkItemId, @ConnectionId, 'completed', 'https://github.com/shiplean-tests/inbox', 'main', now(), @joel, now(), 'propose', now())
            """,
            new { sessionId, issue.WorkItemId, _arranged!.ConnectionId, joel = SeedIds.Users.Joel });
        await database.ExecuteAsync("UPDATE github_issue SET proposal_session_id = @sessionId WHERE id = @Id", new { sessionId, issue.Id });
    }

    private static string ShippedJson(string existing, string requires)
        => $$"""{ "verdict": "alreadyShipped", "type": "story", "title": "Login label", "existing": [{{existing}}], "requires": [{{requires}}] }""";

    // A new item of the arranged project with a github.com pull request link whose link_state is `state` (merged with a merge commit).
    private async Task<(string Key, int Pull, string Sha)> AddMergedItemAsync(string state = "merged")
    {
        var pull = Random.Shared.Next(1, int.MaxValue);
        var sha = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray());
        var url = $"https://github.com/shiplean-tests/inbox/pull/{pull}";
        await using var database = await OpenDatabaseAsync();
        var (item, key, _) = await _arranged!.CreateItemAsync(Api);
        await database.ExecuteAsync(
            """
            INSERT INTO work_item_link (id, source_id, link_type, url, title, created_by, created_at)
            VALUES (@linkId, @item, 'url', @url, 'Pull request', @joel, now())
            """,
            new { linkId = Guid.NewGuid(), item, url, joel = SeedIds.Users.Joel });
        await database.ExecuteAsync(
            """
            INSERT INTO link_state (url_key, url, state, as_of, source, merge_commit_sha, merged_at)
            VALUES (@urlKey, @url, @state, now(), 'github', @sha, now())
            """,
            new { urlKey = $"github.com/shiplean-tests/inbox/pull/{pull}", url, state, sha });
        return (key, pull, sha);
    }

    private async Task ChangedAsync(GitHubIssueRecord issue, IssueAction action, DateTimeOffset at)
    {
        var changed = new IssueEvent(
            action, issue.Issue, $"https://github.com/{issue.Issue.Repository}/issues/{issue.Issue.Number}", issue.Title, issue.Body, "reporter", "NONE", [],
            action != IssueAction.Closed, null, "reporter", false, at);
        await using var scope = Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIssueIntake>().ChangedAsync(changed, CancellationToken.None);
    }

    // What the outbox deliverer does with an entry while the issue is closed on GitHub: finishes it unsent.
    private async Task SkipClosedAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            "UPDATE github_outbox SET sent_at = now(), last_result = 'skipped: issue closed' WHERE github_issue_id = @issueId AND sent_at IS NULL", new { issueId });
    }

    private async Task EnqueueAsync(GitHubIssueRecord issue, Guid sessionId, string body, string? key = null)
    {
        var payload = JsonSerializer.Serialize(new { sessionId, body });
        (await Host.Services.GetRequiredService<IIssueJobs>().EnqueueAsync(
            IssueJobKind.Proposal, key ?? $"proposal:{sessionId}", issue.Id, issue.ProjectId, payload)).ShouldBeTrue();
    }

    private async Task DrainAsync()
    {
        while (await Worker.RunNextAsync(CancellationToken.None))
        {
            // Every due proposal job, other fixtures' leftovers included.
        }
    }

    private async Task<string> RunAsNameAsync()
    {
        await using var database = await OpenDatabaseAsync();
        var name = await database.QuerySingleAsync<string>("SELECT name FROM app_user WHERE id = @joel", new { joel = SeedIds.Users.Joel });
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBeGreaterThanOrEqualTo(2);
        return name;
    }

    private async Task<GitHubIssueRecord> IssueAsync(Guid id) => (await Issues.FindByIdAsync(id)).ShouldNotBeNull();

    private async Task<List<string>> JobStatusesAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<string>(
            "SELECT status FROM issue_job WHERE github_issue_id = @issueId AND kind = 'proposal' ORDER BY created_at", new { issueId })];
    }

    private async Task<List<OutboxRow>> OutboxAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<OutboxRow>(
            "SELECT dedupe_key AS dedupeKey, body, sent_at IS NOT NULL AS sent FROM github_outbox WHERE github_issue_id = @issueId ORDER BY seq", new { issueId })];
    }

    private async Task<List<CommentRow>> ShipLeanCommentsAsync(Guid workItemId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<CommentRow>(
            "SELECT author_id AS authorId, body FROM comment WHERE work_item_id = @workItemId AND agent_name = @AgentName ORDER BY created_at",
            new { workItemId, RoundHolds.AgentName })];
    }

    private sealed record OutboxRow(string DedupeKey, string? Body, bool Sent);

    private sealed record CommentRow(Guid AuthorId, string Body);
}
