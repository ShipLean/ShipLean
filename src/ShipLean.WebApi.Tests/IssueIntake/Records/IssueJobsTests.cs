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
            .InsertAsync(new IssueRef(IssueRef.GitHubHost, "shiplean-tests/jobs", 1), SeedIds.Projects.Ship, SeedIds.WorkItems.Ship89, "reporter", IssuePhase.Proposing);
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

        await _jobs.CompleteAsync(job.Id);
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
            await _jobs.FailAsync(job.Id, "boom");

            _clock.Advance(wait - TimeSpan.FromSeconds(1));
            (await _jobs.ClaimNextAsync(IssueJobKind.Round)).ShouldBeNull($"before the {wait} wait after attempt {job.Attempts}");
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        var last = (await _jobs.ClaimNextAsync(IssueJobKind.Round))!;
        last.Attempts.ShouldBe(IIssueJobs.MaxAttempts);
        await _jobs.FailAsync(last.Id, "boom again");
        _clock.Advance(TimeSpan.FromDays(2));
        (await _jobs.ClaimNextAsync(IssueJobKind.Round)).ShouldBeNull();
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string Status, string LastError)>(
            "SELECT status, last_error FROM issue_job WHERE id = @id", new { id = last.Id });
        row.ShouldBe(("failed", "boom again"));
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
