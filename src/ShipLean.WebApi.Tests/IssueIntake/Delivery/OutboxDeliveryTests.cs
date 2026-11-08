using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Shouldly;
using ShipLean.Application.Features.IssueIntake;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.IssueIntake;

namespace ShipLean.WebApi.Tests.IssueIntake.Delivery;

// The outbox deliverer's passes over the real github_outbox, driven by hand with a fake clock and a recording GitHub. The
// database is shared by the class, so SetUp sets aside entries an earlier test left pending; each test makes its own issues.
[TestFixture]
public class OutboxDeliveryTests : ApiFixture
{
    private static int _number;

    private FakeTimeProvider _clock = null!;
    private RecordingGitHubIssues _recording = null!;
    private IGitHubIssues _github = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IGitHubIdentity>(new FixedGitHubIdentity());
        services.AddScoped(_ => _github);
    };

    private NpgsqlDataSource DataSource => Host.Services.GetRequiredService<NpgsqlDataSource>();

    [SetUp]
    public async Task Arrange()
    {
        // Ahead of the host's clock, so every entry enqueued during the test is already due.
        _clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow() + TimeSpan.FromMinutes(1));
        _recording = new RecordingGitHubIssues();
        _github = _recording;
        await using var connection = await DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "UPDATE github_outbox SET failed_at = now(), last_error = 'Set aside by a test.', leased_until = NULL WHERE sent_at IS NULL AND failed_at IS NULL");
    }

    private GitHubOutboxDeliverer Deliverer()
        => new(DataSource, Host.Services.GetRequiredService<IServiceScopeFactory>(), Host.Services.GetRequiredService<IGitHubIdentity>(), _clock, NullLogger<GitHubOutboxDeliverer>.Instance);

    private async Task<(Guid Id, IssueRef Issue)> NewIssueAsync()
    {
        var issue = new IssueRef(IssueRef.GitHubHost, "shiplean-tests/delivery", Interlocked.Increment(ref _number));
        var record = await Host.Services.GetRequiredService<IGitHubIssueStore>()
            .InsertAsync(issue, SeedIds.Projects.Ship, SeedIds.WorkItems.Ship89, "reporter", "Title", "Body", IssuePhase.AwaitingReply);
        return (record.Id, issue);
    }

    // Enqueues and commits the entry, and returns its id.
    private async Task<Guid> EnqueueAsync(OutboxEntry entry)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            (await Host.Services.GetRequiredService<IGitHubOutbox>().EnqueueAsync(entry, transaction)).ShouldBeTrue();
            await transaction.CommitAsync();
        }

        return await connection.ExecuteScalarAsync<Guid>("SELECT id FROM github_outbox WHERE dedupe_key = @key", new { key = entry.DedupeKey });
    }

    private static string Key(string what) => $"test:{what}:{Guid.NewGuid():N}";

    private async Task<IReadOnlyList<Row>> RowsAsync(Guid issueId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        var rows = await connection.QueryAsync<Row>(
            """
            SELECT kind, sent_at IS NOT NULL AS sent, failed_at IS NOT NULL AS failed, github_comment_id AS commentId, last_result AS lastResult,
                   last_error AS lastError, attempts, next_attempt_at AS nextAttemptAt
            FROM github_outbox WHERE github_issue_id = @issueId ORDER BY seq
            """,
            new { issueId });
        return [.. rows];
    }

    private async Task<int> NotesAsync()
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM comment WHERE work_item_id = @id AND agent_name = @name",
            new { id = SeedIds.WorkItems.Ship89, name = GitHubOutboxDeliverer.NoteAgentName });
    }

    [Test]
    public async Task Deliver_WhenTwoWorkers_ShouldSendInOrderOnce()
    {
        var (first, firstIssue) = await NewIssueAsync();
        var (second, secondIssue) = await NewIssueAsync();
        await EnqueueAsync(OutboxEntry.Comment(first, Key("a"), "First update."));
        await EnqueueAsync(OutboxEntry.Comment(first, Key("b"), "Second update."));
        await EnqueueAsync(OutboxEntry.Close(first, Key("close"), CloseReason.Completed));
        await EnqueueAsync(OutboxEntry.Comment(second, Key("c"), "Other issue."));
        var gate = new GatedGitHubIssues(_recording);
        _github = gate;

        // Worker one claims the first issue's oldest entry and is held mid-send.
        var one = Task.Run(() => Deliverer().RunDueAsync(CancellationToken.None));
        await gate.Entered.Task;
        // Worker two runs meanwhile: the first issue is leased, so it sends only the other issue's entry.
        (await Deliverer().RunDueAsync(CancellationToken.None)).ShouldBe(1);
        _recording.Calls.ShouldBe([$"comment {secondIssue}"]);

        gate.Release.SetResult();
        (await one).ShouldBe(3);
        (await Deliverer().RunDueAsync(CancellationToken.None)).ShouldBe(0);

        _recording.Calls.ShouldBe([$"comment {secondIssue}", $"comment {firstIssue}", $"comment {firstIssue}", $"close {firstIssue} completed"]);
        _recording.CommentBodies[1].ShouldStartWith("First update.");
        _recording.CommentBodies[2].ShouldStartWith("Second update.");
        (await RowsAsync(first)).ShouldAllBe(r => r.Sent && r.Attempts == 1);
    }

    [Test]
    public async Task Deliver_WhenAmbiguousAndPosted_ShouldNotPostAgain()
    {
        var (issueId, issue) = await NewIssueAsync();
        var entryId = await EnqueueAsync(OutboxEntry.Comment(issueId, Key("ambiguous"), "Proposal."));
        _recording.QueueResult(new GitHubWriteResult.Ambiguous("timeout"));
        await Deliverer().RunDueAsync(CancellationToken.None);
        (await RowsAsync(issueId)).ShouldHaveSingleItem().LastResult.ShouldBe("ambiguous");

        // GitHub did make it.
        _recording.AddExistingComment(issue, new GitHubComment(4242, FixedGitHubIdentity.DefaultLogin, IssueMarkdown.WithMarker("Proposal.", entryId), TimeProvider.System.GetUtcNow()));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await Deliverer().RunDueAsync(CancellationToken.None);

        _recording.Calls.ShouldBe([$"comment {issue}", $"list {issue}"]);
        var row = (await RowsAsync(issueId)).ShouldHaveSingleItem();
        row.Sent.ShouldBeTrue();
        row.CommentId.ShouldBe(4242);
    }

    [Test]
    public async Task Deliver_WhenAmbiguousAndMissing_ShouldRetry()
    {
        var (issueId, issue) = await NewIssueAsync();
        await EnqueueAsync(OutboxEntry.Comment(issueId, Key("missing"), "Proposal."));
        _recording.QueueResult(new GitHubWriteResult.Ambiguous("500"));
        await Deliverer().RunDueAsync(CancellationToken.None);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await Deliverer().RunDueAsync(CancellationToken.None);

        _recording.Calls.ShouldBe([$"comment {issue}", $"list {issue}", $"comment {issue}"]);
        var row = (await RowsAsync(issueId)).ShouldHaveSingleItem();
        row.Sent.ShouldBeTrue();
        row.CommentId.ShouldNotBeNull();
    }

    [Test]
    public async Task Deliver_WhenAmbiguousAndListFails_ShouldStayAmbiguous()
    {
        var (issueId, issue) = await NewIssueAsync();
        await EnqueueAsync(OutboxEntry.Comment(issueId, Key("unlisted"), "Proposal."));
        _recording.QueueResult(new GitHubWriteResult.Ambiguous("timeout"));
        await Deliverer().RunDueAsync(CancellationToken.None);

        _recording.QueueList(new GitHubCommentList.Retry("unreadableReply"));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await Deliverer().RunDueAsync(CancellationToken.None);

        // The list could not be read, so it must not post again, and the next try reconciles again.
        _recording.Calls.ShouldBe([$"comment {issue}", $"list {issue}"]);
        var row = (await RowsAsync(issueId)).ShouldHaveSingleItem();
        row.Sent.ShouldBeFalse();
        row.LastResult.ShouldBe("ambiguous");
    }

    [Test]
    public async Task Deliver_WhenMarkerByOtherUser_ShouldNotCountAsSent()
    {
        var (issueId, issue) = await NewIssueAsync();
        var entryId = await EnqueueAsync(OutboxEntry.Comment(issueId, Key("forged"), "Proposal."));
        _recording.QueueResult(new GitHubWriteResult.Ambiguous("timeout"));
        await Deliverer().RunDueAsync(CancellationToken.None);

        // Someone else typed the marker.
        _recording.AddExistingComment(issue, new GitHubComment(777, "mallory", IssueMarkdown.OutboxMarker(entryId), TimeProvider.System.GetUtcNow()));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await Deliverer().RunDueAsync(CancellationToken.None);

        _recording.Calls.ShouldBe([$"comment {issue}", $"list {issue}", $"comment {issue}"]);
        var row = (await RowsAsync(issueId)).ShouldHaveSingleItem();
        row.Sent.ShouldBeTrue();
        row.CommentId.ShouldNotBe(777);
    }

    [Test]
    public async Task Deliver_WhenLeaseExpires_ShouldResume()
    {
        var (issueId, issue) = await NewIssueAsync();
        await EnqueueAsync(OutboxEntry.Comment(issueId, Key("lease"), "Update."));
        var gate = new GatedGitHubIssues(_recording);
        _github = gate;

        // Worker one claims the entry and stalls past its lease.
        var one = Task.Run(() => Deliverer().RunDueAsync(CancellationToken.None));
        await gate.Entered.Task;
        (await Deliverer().RunDueAsync(CancellationToken.None)).ShouldBe(0);
        _clock.Advance(GitHubOutboxDeliverer.Lease + TimeSpan.FromSeconds(1));

        // Worker two takes it over and sends it.
        (await Deliverer().RunDueAsync(CancellationToken.None)).ShouldBe(1);
        var sent = (await RowsAsync(issueId)).ShouldHaveSingleItem();
        sent.Sent.ShouldBeTrue();
        sent.Attempts.ShouldBe(2);

        // Worker one's late answer changes nothing: the entry is worker two's.
        gate.Release.SetResult();
        await one;
        (await RowsAsync(issueId)).ShouldHaveSingleItem().CommentId.ShouldBe(sent.CommentId);
        _recording.Calls.ShouldBe([$"comment {issue}", $"comment {issue}"]);
    }

    [Test]
    public async Task Deliver_WhenStaleWorkerAnswers_ShouldNotTouchNewClaim()
    {
        var (issueId, issue) = await NewIssueAsync();
        await EnqueueAsync(OutboxEntry.Comment(issueId, Key("stale"), "Update."));
        // Worker one's call waits on the outer gate, worker two's on the inner one.
        var inner = new GatedGitHubIssues(_recording);
        var outer = new GatedGitHubIssues(inner);
        _github = outer;

        var one = Task.Run(() => Deliverer().RunDueAsync(CancellationToken.None));
        await outer.Entered.Task;
        _clock.Advance(GitHubOutboxDeliverer.Lease + TimeSpan.FromSeconds(1));
        var two = Task.Run(() => Deliverer().RunDueAsync(CancellationToken.None));
        await inner.Entered.Task;

        // Worker one answers late, with a result that would free the entry; worker two still holds the claim.
        _recording.QueueResult(new GitHubWriteResult.Retry("502"));
        outer.Release.SetResult();
        await one;
        var held = (await RowsAsync(issueId)).ShouldHaveSingleItem();
        held.LastResult.ShouldBeNull();
        held.Attempts.ShouldBe(2);

        inner.Release.SetResult();
        await two;
        (await RowsAsync(issueId)).ShouldHaveSingleItem().Sent.ShouldBeTrue();
        _recording.Calls.ShouldBe([$"comment {issue}", $"comment {issue}"]);
    }

    [Test]
    public async Task Deliver_WhenCommentFails_ShouldHoldClose()
    {
        var (issueId, issue) = await NewIssueAsync();
        await EnqueueAsync(OutboxEntry.Comment(issueId, Key("fails"), "Done in SHIP-1."));
        await EnqueueAsync(OutboxEntry.Close(issueId, Key("held"), CloseReason.Completed));
        var notes = await NotesAsync();
        _recording.QueueResult(new GitHubWriteResult.Failed("422"));

        await Deliverer().RunDueAsync(CancellationToken.None);

        _recording.Calls.ShouldBe([$"comment {issue}"]);
        var rows = await RowsAsync(issueId);
        rows.Select(r => (r.Kind, r.Failed, r.LastResult, r.LastError)).ShouldBe([("comment", true, "failed", "422"), ("close", true, "failed", "previous update failed")]);
        (await NotesAsync()).ShouldBe(notes + 1);
    }

    [Test]
    public async Task Deliver_WhenIssueClosed_ShouldSkipComments()
    {
        var (issueId, issue) = await NewIssueAsync();
        await Host.Services.GetRequiredService<IGitHubIssueStore>().SetOpenOnGitHubAsync(issueId, false, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
        await EnqueueAsync(OutboxEntry.Comment(issueId, Key("closed"), "Update."));
        await EnqueueAsync(OutboxEntry.Reopen(issueId, Key("reopen")));
        var notes = await NotesAsync();

        await Deliverer().RunDueAsync(CancellationToken.None);

        _recording.Calls.ShouldBe([$"reopen {issue}"]);
        var rows = await RowsAsync(issueId);
        rows.Select(r => (r.Kind, r.Sent, r.Failed, r.LastError)).ShouldBe([("comment", false, true, "issue closed"), ("reopen", true, false, (string?)null)]);
        (await NotesAsync()).ShouldBe(notes);
    }

    [Test]
    public async Task Deliver_WhenRetry_ShouldBackOff()
    {
        var (issueId, _) = await NewIssueAsync();
        await EnqueueAsync(OutboxEntry.Comment(issueId, Key("retry"), "Update."));
        await EnqueueAsync(OutboxEntry.Comment(issueId, Key("behind"), "Next."));
        var notes = await NotesAsync();
        TimeSpan[] waits = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1), TimeSpan.FromHours(6), TimeSpan.FromHours(6), TimeSpan.FromHours(6), TimeSpan.FromHours(6)];

        foreach (var wait in waits)
        {
            _recording.QueueResult(new GitHubWriteResult.Retry("429"));
            (await Deliverer().RunDueAsync(CancellationToken.None)).ShouldBe(1);
            var head = (await RowsAsync(issueId))[0];
            head.LastResult.ShouldBe("retry");
            head.NextAttemptAt.ShouldBe(_clock.GetUtcNow() + wait, TimeSpan.FromMilliseconds(1));
            // Not due yet, and the entry behind it waits.
            (await Deliverer().RunDueAsync(CancellationToken.None)).ShouldBe(0);
            _clock.Advance(wait);
        }

        _recording.QueueResult(new GitHubWriteResult.Retry("429"));
        _recording.QueueResult(new GitHubWriteResult.Ok(99));
        (await Deliverer().RunDueAsync(CancellationToken.None)).ShouldBe(2);

        _recording.Calls.Count.ShouldBe(9);
        var rows = await RowsAsync(issueId);
        rows.Select(r => (r.Attempts, r.Sent, r.Failed, r.LastResult)).ShouldBe([(GitHubOutboxDeliverer.MaxAttempts, false, true, "retry"), (1, true, false, "ok")]);
        (await NotesAsync()).ShouldBe(notes + 1);
    }

    private sealed record Row(string Kind, bool Sent, bool Failed, long? CommentId, string? LastResult, string? LastError, int Attempts, DateTimeOffset NextAttemptAt);
}
