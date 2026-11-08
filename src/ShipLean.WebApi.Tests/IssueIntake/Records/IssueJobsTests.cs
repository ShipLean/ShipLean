using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Shouldly;
using ShipLean.Application.Features.IssueIntake.Records;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.IssueIntake.Records;

// The real issue_job store on a seeded clone, on a fake clock each test moves itself. A claim takes any due job of a kind,
// so each test ends by completing every pending job it may have left behind.
[TestFixture]
public class IssueJobsTests : ApiFixture
{
    private FakeTimeProvider _clock = null!;
    private DbIssueJobs _jobs = null!;
    private Guid _issueId;

    private NpgsqlDataSource DataSource => Host.Services.GetRequiredService<NpgsqlDataSource>();

    [OneTimeSetUp]
    public async Task InsertIssueAsync()
    {
        var issue = await Host.Services.GetRequiredService<IGitHubIssueStore>()
            .InsertAsync(new IssueRef(IssueRef.GitHubHost, "shiplean-tests/jobs", 1), SeedIds.Projects.Ship, SeedIds.WorkItems.Ship89, "reporter", "Title", "Body", IssuePhase.Proposing);
        _issueId = issue.Id;
    }

    [SetUp]
    public void NewClock()
    {
        _clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        _jobs = new DbIssueJobs(DataSource, _clock);
    }

    [TearDown]
    public async Task CompleteLeftoversAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE issue_job SET status = 'done', done_at = now() WHERE status = 'pending'");
    }

    private static string NewKey(string what) => $"round:{what}:{Guid.NewGuid():N}";

    private Task<bool> EnqueueAsync(string key, string? payload = null)
        => _jobs.EnqueueAsync(IssueJobKind.Round, key, _issueId, SeedIds.Projects.Ship, payload);

    [Test]
    public async Task Jobs_WhenEnqueuedTwice_ShouldKeepOne()
    {
        var key = NewKey("twice");

        (await EnqueueAsync(key, """{"n":1}""")).ShouldBeTrue();
        (await EnqueueAsync(key, """{"n":2}""")).ShouldBeFalse();

        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<string>("SELECT payload::text FROM issue_job WHERE key = @key", new { key })).ShouldBe("""{"n": 1}""");
    }

    [Test]
    public async Task Jobs_WhenTwoWorkersClaim_ShouldClaimOnce()
    {
        var key = NewKey("workers");
        await EnqueueAsync(key);

        var claims = await Task.WhenAll(_jobs.ClaimNextAsync(IssueJobKind.Round), _jobs.ClaimNextAsync(IssueJobKind.Round));

        claims.Count(c => c is not null).ShouldBe(1);
        var claimed = claims.Single(c => c is not null)!;
        claimed.ShouldSatisfyAllConditions(
            () => claimed.Key.ShouldBe(key),
            () => claimed.Kind.ShouldBe(IssueJobKind.Round),
            () => claimed.GitHubIssueId.ShouldBe(_issueId),
            () => claimed.ProjectId.ShouldBe(SeedIds.Projects.Ship),
            () => claimed.Payload.ShouldBe("{}"),
            () => claimed.Attempts.ShouldBe(1));
    }

    [Test]
    public async Task Jobs_WhenOtherKind_ShouldNotClaim()
    {
        await EnqueueAsync(NewKey("kind"));

        (await _jobs.ClaimNextAsync(IssueJobKind.Proposal)).ShouldBeNull();
    }

    [Test]
    public async Task Jobs_WhenLeaseExpires_ShouldReclaim()
    {
        var key = NewKey("lease");
        await EnqueueAsync(key);
        var first = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;

        _clock.Advance(IIssueJobs.Lease - TimeSpan.FromSeconds(1));
        (await _jobs.ClaimNextAsync(IssueJobKind.Round)).ShouldBeNull();
        _clock.Advance(TimeSpan.FromSeconds(1));
        var second = await _jobs.ClaimNextAsync(IssueJobKind.Round);

        second.ShouldNotBeNull();
        second.Id.ShouldBe(first.Id);
        second.Attempts.ShouldBe(2);
    }

    [Test]
    public async Task Jobs_WhenCompleted_ShouldNotBeClaimedAgain()
    {
        await EnqueueAsync(NewKey("complete"));
        var job = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;

        (await _jobs.CompleteAsync(job.Id, job.LeaseToken)).ShouldBeTrue();
        _clock.Advance(TimeSpan.FromDays(1));

        (await _jobs.ClaimNextAsync(IssueJobKind.Round)).ShouldBeNull();
    }

    [Test]
    public async Task Jobs_WhenFailed_ShouldBackOff()
    {
        await EnqueueAsync(NewKey("backoff"));
        TimeSpan[] waits = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1), TimeSpan.FromHours(6), TimeSpan.FromHours(6)];

        foreach (var wait in waits)
        {
            var job = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;
            (await _jobs.FailAsync(job.Id, job.LeaseToken, "boom")).ShouldBeTrue();

            _clock.Advance(wait - TimeSpan.FromSeconds(1));
            (await _jobs.ClaimNextAsync(IssueJobKind.Round)).ShouldBeNull($"before the {wait} wait after attempt {job.Attempts}");
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        var last = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;
        last.Attempts.ShouldBe(IIssueJobs.MaxAttempts);
        (await _jobs.FailAsync(last.Id, last.LeaseToken, "boom again")).ShouldBeTrue();
        _clock.Advance(TimeSpan.FromDays(2));
        (await _jobs.ClaimNextAsync(IssueJobKind.Round)).ShouldBeNull();
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string Status, string LastError)>(
            "SELECT status, last_error FROM issue_job WHERE id = @id", new { id = last.Id });
        row.ShouldBe(("failed", "boom again"));
    }

    [Test]
    public async Task Jobs_WhenFinalLeaseExpires_ShouldFail()
    {
        await EnqueueAsync(NewKey("final-lease"));
        IssueJob job = null!;
        for (var attempt = 1; attempt <= IIssueJobs.MaxAttempts; attempt++)
        {
            job = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;
            job.Attempts.ShouldBe(attempt);
            _clock.Advance(IIssueJobs.Lease);
        }

        (await _jobs.ClaimNextAsync(IssueJobKind.Round)).ShouldBeNull();

        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string Status, string LastError, bool Done)>(
            "SELECT status, last_error, done_at IS NOT NULL FROM issue_job WHERE id = @id", new { id = job.Id });
        row.ShouldBe(("failed", "The lease expired on the last attempt.", true));
    }

    [Test]
    public async Task Complete_WhenLeaseWasReclaimed_ShouldReturnFalse()
    {
        await EnqueueAsync(NewKey("complete-stale"));
        var first = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;
        _clock.Advance(IIssueJobs.Lease);
        var second = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;

        (await _jobs.CompleteAsync(first.Id, first.LeaseToken)).ShouldBeFalse();

        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<string>("SELECT status FROM issue_job WHERE id = @id", new { id = second.Id })).ShouldBe("pending");
        (await _jobs.CompleteAsync(second.Id, second.LeaseToken)).ShouldBeTrue();
    }

    [Test]
    public async Task Fail_WhenLeaseWasReclaimed_ShouldReturnFalse()
    {
        await EnqueueAsync(NewKey("fail-stale"));
        var first = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;
        _clock.Advance(IIssueJobs.Lease);
        var second = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;

        (await _jobs.FailAsync(first.Id, first.LeaseToken, "late")).ShouldBeFalse();

        (await _jobs.ClaimNextAsync(IssueJobKind.Round)).ShouldBeNull("the second worker's lease still holds");
        await using var connection = await OpenDatabaseAsync();
        var lastError = await connection.QuerySingleAsync<string?>("SELECT last_error FROM issue_job WHERE id = @id", new { id = second.Id });
        lastError.ShouldBeNull();
        (await _jobs.CompleteAsync(second.Id, second.LeaseToken)).ShouldBeTrue();
    }

    [Test]
    public async Task Fail_WhenCompletedConcurrently_ShouldNotResurrect()
    {
        await EnqueueAsync(NewKey("race"));
        var job = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;
        await using var completer = await OpenDatabaseAsync();
        await using var transaction = await completer.BeginTransactionAsync();
        (await _jobs.CompleteAsync(job.Id, job.LeaseToken, transaction)).ShouldBeTrue();

        // The fail waits on the completing transaction's row lock, then re-reads the row it updates once that commits.
        var fail = _jobs.FailAsync(job.Id, job.LeaseToken, "late");
        await WaitForLockWaitAsync();
        await transaction.CommitAsync();

        (await fail).ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string Status, string? LastError)>(
            "SELECT status, last_error FROM issue_job WHERE id = @id", new { id = job.Id });
        row.ShouldBe(("done", (string?)null));
    }

    // Polls the server (each poll a round trip, no sleep) until a backend is blocked on an issue_job row lock.
    private async Task WaitForLockWaitAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!timeout.IsCancellationRequested)
        {
            var waiting = await connection.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query ILIKE '%UPDATE issue_job%'");
            if (waiting > 0)
            {
                return;
            }

            await Task.Yield();
        }

        throw new TimeoutException("The fail never waited on the completing transaction's row lock.");
    }

    [Test]
    public async Task CountSince_WhenJobsBeforeAndAfter_ShouldCountOnlyLaterOfThatKind()
    {
        var projectId = SeedIds.Projects.Ship;
        await EnqueueAsync(NewKey("old"));
        _clock.Advance(TimeSpan.FromHours(2));
        var since = _clock.GetUtcNow();
        await EnqueueAsync(NewKey("new-1"));
        await EnqueueAsync(NewKey("new-2"));
        await _jobs.EnqueueAsync(IssueJobKind.Accept, $"accept:{Guid.NewGuid():N}", _issueId, projectId, null);

        (await _jobs.CountSinceAsync(projectId, IssueJobKind.Round, since)).ShouldBe(2);
    }
}
