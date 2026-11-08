using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.IssueIntake.Outbox;

// The real outbox enqueue (DbGitHubOutbox) on a seeded clone. Each test makes its own issues and dedupe keys.
[TestFixture]
public class EnqueueTests : ApiFixture
{
    private static int _number;

    private IGitHubOutbox Outbox => Host.Services.GetRequiredService<IGitHubOutbox>();

    private NpgsqlDataSource DataSource => Host.Services.GetRequiredService<NpgsqlDataSource>();

    private static string NewKey(string what) => $"test:{what}:{Guid.NewGuid():N}";

    private async Task<Guid> NewIssueAsync()
    {
        var issue = new IssueRef(IssueRef.GitHubHost, "shiplean-tests/outbox", Interlocked.Increment(ref _number));
        var record = await Host.Services.GetRequiredService<IGitHubIssueStore>()
            .InsertAsync(issue, SeedIds.Projects.Ship, SeedIds.WorkItems.Ship89, "reporter", IssuePhase.Proposing);
        return record.Id;
    }

    private async Task<bool> EnqueueCommittedAsync(OutboxEntry entry)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var added = await Outbox.EnqueueAsync(entry, transaction);
        await transaction.CommitAsync();
        return added;
    }

    private async Task<IReadOnlyList<(string DedupeKey, string Kind, string? Body, string? Reason, long Seq)>> RowsAsync(Guid issueId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        var rows = await connection.QueryAsync<(string, string, string?, string?, long)>(
            "SELECT dedupe_key, kind, body, reason, seq FROM github_outbox WHERE github_issue_id = @issueId ORDER BY seq", new { issueId });
        return [.. rows];
    }

    [Test]
    public async Task Enqueue_WhenDuplicateKey_ShouldKeepOne()
    {
        var issueId = await NewIssueAsync();
        var key = NewKey("duplicate");

        (await EnqueueCommittedAsync(OutboxEntry.Comment(issueId, key, "First"))).ShouldBeTrue();
        (await EnqueueCommittedAsync(OutboxEntry.Comment(issueId, key, "Second"))).ShouldBeFalse();

        var row = (await RowsAsync(issueId)).ShouldHaveSingleItem();
        row.DedupeKey.ShouldBe(key);
        row.Body.ShouldBe("First");
    }

    [Test]
    public async Task Enqueue_WhenTransactionRollsBack_ShouldKeepNone()
    {
        var issueId = await NewIssueAsync();

        await using (var connection = await DataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            (await Outbox.EnqueueAsync(OutboxEntry.Comment(issueId, NewKey("rollback"), "Hello"), transaction)).ShouldBeTrue();
            await transaction.RollbackAsync();
        }

        (await RowsAsync(issueId)).ShouldBeEmpty();
    }

    [Test]
    public async Task Enqueue_ShouldStoreEachKindDueNow()
    {
        var issueId = await NewIssueAsync();

        await EnqueueCommittedAsync(OutboxEntry.Comment(issueId, NewKey("comment"), "Done."));
        await EnqueueCommittedAsync(OutboxEntry.Close(issueId, NewKey("close"), CloseReason.NotPlanned));
        await EnqueueCommittedAsync(OutboxEntry.Reopen(issueId, NewKey("reopen")));

        (await RowsAsync(issueId)).Select(r => (r.Kind, r.Body, r.Reason)).ShouldBe([("comment", "Done.", null), ("close", null, "not_planned"), ("reopen", null, null)]);
        await using var connection = await DataSource.OpenConnectionAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM github_outbox WHERE github_issue_id = @issueId AND next_attempt_at = created_at AND attempts = 0 AND sent_at IS NULL", new { issueId })).ShouldBe(3);
    }

    [Test]
    public async Task Enqueue_WhenCommentHasNoBody_ShouldThrow()
    {
        var issueId = await NewIssueAsync();
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await Should.ThrowAsync<ArgumentException>(() => Outbox.EnqueueAsync(new OutboxEntry(issueId, OutboxKind.Comment, NewKey("nobody")), transaction));
    }

    [Test]
    public async Task Enqueue_WhenConcurrentForOneIssue_ShouldCommitInSeqOrder()
    {
        var issueId = await NewIssueAsync();
        var otherIssueId = await NewIssueAsync();
        await using var firstConnection = await DataSource.OpenConnectionAsync();
        await using var secondConnection = await DataSource.OpenConnectionAsync();
        await using var otherConnection = await DataSource.OpenConnectionAsync();
        await using var firstTransaction = await firstConnection.BeginTransactionAsync();
        await using var secondTransaction = await secondConnection.BeginTransactionAsync();
        await using var otherTransaction = await otherConnection.BeginTransactionAsync();

        await Outbox.EnqueueAsync(OutboxEntry.Comment(issueId, NewKey("first"), "Done."), firstTransaction);
        // The second enqueue for the same issue waits for the first transaction; one for another issue does not.
        var second = Outbox.EnqueueAsync(OutboxEntry.Close(issueId, NewKey("second"), CloseReason.Completed), secondTransaction);
        (await Outbox.EnqueueAsync(OutboxEntry.Comment(otherIssueId, NewKey("other"), "Hi"), otherTransaction)).ShouldBeTrue();
        (await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(500)))).ShouldNotBe(second);

        await firstTransaction.CommitAsync();
        (await second).ShouldBeTrue();
        await secondTransaction.CommitAsync();
        await otherTransaction.CommitAsync();

        var rows = await RowsAsync(issueId);
        rows.Select(r => r.Kind).ShouldBe(["comment", "close"]);
        rows[0].Seq.ShouldBeLessThan(rows[1].Seq);
    }
}
