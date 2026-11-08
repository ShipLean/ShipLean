using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Application.Features.IssueIntake.Inbox;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Application.Features.IssueIntake.Rounds;
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

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<FixedIssueIntakeSettings>();
        services.AddScoped<IIssueIntakeSettings>(sp => sp.GetRequiredService<FixedIssueIntakeSettings>());
        services.AddSingleton<DbGitHubOutbox>();
        services.AddSingleton<CrashingGitHubOutbox>();
        services.AddSingleton<IGitHubOutbox>(sp => sp.GetRequiredService<CrashingGitHubOutbox>());
    };

    private ProposalJobWorker Worker => Host.Services.GetRequiredService<ProposalJobWorker>();

    private IGitHubIssueStore Issues => Host.Services.GetRequiredService<IGitHubIssueStore>();

    [SetUp]
    [TearDown]
    public async Task CompleteLeftoversAsync()
    {
        Host.Services.GetRequiredService<CrashingGitHubOutbox>().CrashAfterEnqueue = false;
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE status = 'pending' AND kind = 'proposal'");
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

        await EnqueueAsync(issue, sessionId, response, key: $"proposal:{sessionId}:again");
        await DrainAsync();

        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem();
        (await IssueAsync(issue.Id)).Phase.ShouldBe(IssuePhase.AwaitingReply);
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
        (await OutboxAsync(issue.Id)).ShouldHaveSingleItem().Body.ShouldBe($"This already shipped.\n\n{ProposalJobWorker.Footer}");
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
            await database.ExecuteAsync(
                """
                INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, created_by, created_at, mode, ended_at)
                VALUES (@sessionId, @item, @ConnectionId, 'completed', 'https://github.com/shiplean-tests/inbox', 'main', now(), @joel, now(), 'propose', now())
                """,
                new { sessionId, item, arranged.ConnectionId, joel = SeedIds.Users.Joel });
        }

        return (issue, sessionId);
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
            "SELECT dedupe_key AS dedupeKey, body FROM github_outbox WHERE github_issue_id = @issueId ORDER BY seq", new { issueId })];
    }

    private async Task<List<CommentRow>> ShipLeanCommentsAsync(Guid workItemId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<CommentRow>(
            "SELECT author_id AS authorId, body FROM comment WHERE work_item_id = @workItemId AND agent_name = @AgentName ORDER BY created_at",
            new { workItemId, RoundHolds.AgentName })];
    }

    private sealed record OutboxRow(string DedupeKey, string? Body);

    private sealed record CommentRow(Guid AuthorId, string Body);
}
