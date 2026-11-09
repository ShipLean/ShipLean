using Dapper;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Backups.Modules;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.LocalEnvironment.Database;
using ShipLean.WebApi.Tests.Schema;

namespace ShipLean.WebApi.Tests.Backups.Runs;

// The backup run log on Postgres (contract C-RUN, decision D8): the lease, its loss check, the occurrence guard, pruning
// claims and the settings row. Each test gets an empty database of its own, because the lease is one lock per database.
[TestFixture]
public class BackupRunLogTests
{
    // BackupLeaseLock's two-int key: pg_locks shows it as classid / objid with objsubid 2.
    private const int LOCK_NAMESPACE = 0x42_4B_55_50;
    private const string DestinationA = "destination-a";
    private const string DestinationB = "destination-b";

    private static readonly DateTimeOffset Start = new(2026, 10, 24, 14, 0, 0, TimeSpan.Zero);

    private readonly List<BackupLease> _leases = [];
    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private FakeTimeProvider _clock = null!;
    private BackupRunLog _log = null!;

    [SetUp]
    public async Task CloneDatabase()
    {
        _database = await ShipLeanDatabase.CloneEmptyAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        _clock = new FakeTimeProvider(Start);
        _log = NewInstance();
    }

    [TearDown]
    public async Task DropDatabase()
    {
        foreach (var lease in _leases)
        {
            await lease.DisposeAsync();
        }
        _leases.Clear();
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Test]
    public async Task TryStart_WhenLeaseHeld_ShouldRefuseBusy()
    {
        await StartAsync(Manual());

        var second = await NewInstance().TryStartAsync(Manual());

        second.ShouldBe(new BackupStartResult(null, BackupRefusal.Busy));
        (await _log.RecentAsync(10)).Count.ShouldBe(1);
    }

    [Test]
    public async Task TryStart_WhenOccurrenceExists_ShouldRefuseOccurrenceTaken()
    {
        var occurrence = Start.AddHours(-12);
        await using (var first = await StartAsync(Scheduled(occurrence)))
        {
            (await _log.SucceedAsync(first.Run.Id, Outcome())).ShouldBeTrue();
        }

        var again = await NewInstance().TryStartAsync(Scheduled(occurrence));

        again.ShouldBe(new BackupStartResult(null, BackupRefusal.OccurrenceTaken));
        (await HeldLocksAsync()).ShouldBe(0);
        (await StartAsync(Scheduled(occurrence.AddDays(1)))).Run.ScheduledFor.ShouldBe(occurrence.AddDays(1));
    }

    [Test]
    public async Task Lease_WhenConnectionTerminated_ShouldSignalLost()
    {
        var lease = await StartAsync(Manual());
        var lost = new TaskCompletionSource();
        await using var registration = lease.Lost.Register(() => lost.TrySetResult());
        _clock.Advance(BackupLease.CheckInterval);
        await lease.EnsureHeldAsync();
        lease.Lost.IsCancellationRequested.ShouldBeFalse();

        await TerminateLeaseConnectionAsync();
        _clock.Advance(BackupLease.CheckInterval);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var onTimeout = timeout.Token.Register(() => lost.TrySetCanceled());
        await lost.Task;
        lease.Lost.IsCancellationRequested.ShouldBeTrue();
    }

    [Test]
    public async Task EnsureHeld_WhenLost_ShouldThrow()
    {
        var lease = await StartAsync(Manual());
        await TerminateLeaseConnectionAsync();

        await Should.ThrowAsync<BackupLeaseLostException>(() => lease.EnsureHeldAsync());

        lease.Lost.IsCancellationRequested.ShouldBeTrue();
        await Should.ThrowAsync<BackupLeaseLostException>(() => lease.EnsureHeldAsync());
    }

    [Test]
    public async Task Unpruned_WhenOtherDestination_ShouldExclude()
    {
        var a1 = await SucceededRunAsync(DestinationA);
        var b1 = await SucceededRunAsync(DestinationB);
        var a2 = await SucceededRunAsync(DestinationA);

        (await _log.UnprunedSuccessesAsync(DestinationA)).Select(r => r.Id).ShouldBe([a2, a1]);
        (await _log.UnprunedSuccessesAsync(DestinationB)).Select(r => r.Id).ShouldBe([b1]);
        (await _log.UnprunedSuccessesAsync("destination-c")).ShouldBeEmpty();
    }

    [Test]
    public async Task SettingsRows_WhenWritten_ShouldReadBack()
    {
        var rows = new BackupSettingsRows(_dataSource);
        (await rows.ReadAsync()).ShouldBeNull();
        var first = Settings(Start, generation: 1);

        (await rows.TryWriteAsync(first, null)).ShouldBeTrue();

        (await rows.ReadAsync()).ShouldBe(first);
        var second = first with { Enabled = false, Cron = "30 3 * * *", KeepCount = 30, PassphraseGeneration = 2, UpdatedAt = Start.AddMinutes(5) };
        (await rows.TryWriteAsync(second, first.UpdatedAt)).ShouldBeTrue();
        (await rows.ReadAsync()).ShouldBe(second);
    }

    [Test]
    public async Task SettingsRows_WhenStaleWrite_ShouldReturnFalse()
    {
        var rows = new BackupSettingsRows(_dataSource);
        var first = Settings(Start, generation: 1);
        (await rows.TryWriteAsync(first, null)).ShouldBeTrue();
        var winner = first with { PassphraseGeneration = 2, UpdatedAt = Start.AddMinutes(1) };
        (await rows.TryWriteAsync(winner, first.UpdatedAt)).ShouldBeTrue();

        var stale = first with { PassphraseGeneration = 2, Passphrase = "protected-other", UpdatedAt = Start.AddMinutes(2) };
        (await rows.TryWriteAsync(stale, first.UpdatedAt)).ShouldBeFalse();
        (await rows.TryWriteAsync(stale, null)).ShouldBeFalse();
        (await rows.TryWriteAsync(stale with { UpdatedAt = winner.UpdatedAt }, winner.UpdatedAt)).ShouldBeFalse();

        (await rows.ReadAsync()).ShouldBe(winner);
    }

    [Test]
    public async Task RecordSkipped_WhenTwice_ShouldKeepOne()
    {
        var occurrence = Start.AddHours(-1);

        await _log.RecordSkippedAsync(occurrence);
        await _log.RecordSkippedAsync(occurrence);

        await using var connection = await _database.OpenAsync();
        var states = (await connection.QueryAsync<string>("SELECT state FROM backup_run WHERE scheduled_for = @occurrence", new { occurrence })).ToList();
        states.ShouldBe(["skipped"]);
        (await _log.LatestOccurrenceAsync()).ShouldBe(occurrence);
        (await _log.RecentAsync(10)).ShouldBeEmpty();
        (await _log.TryStartAsync(Scheduled(occurrence))).Refusal.ShouldBe(BackupRefusal.OccurrenceTaken);
    }

    [Test]
    public async Task ClaimPrune_WhenLeaseLost_ShouldThrowAndClaimNothing()
    {
        await SucceededRunAsync(DestinationA);
        await SucceededRunAsync(DestinationA);
        await SucceededRunAsync(DestinationA);
        var lease = await StartAsync(Manual(DestinationA));
        await TerminateLeaseConnectionAsync();

        await Should.ThrowAsync<BackupLeaseLostException>(() => _log.ClaimPruneAsync(lease, DestinationA, 1));

        (await _log.UnprunedSuccessesAsync(DestinationA)).Count.ShouldBe(3);
        lease.Lost.IsCancellationRequested.ShouldBeTrue();
    }

    [Test]
    public async Task ClaimPrune_WhenOverKeep_ShouldClaimOldestOnly()
    {
        var a1 = await SucceededRunAsync(DestinationA);
        var a2 = await SucceededRunAsync(DestinationA);
        var b1 = await SucceededRunAsync(DestinationB);
        var a3 = await SucceededRunAsync(DestinationA);
        var a4 = await SucceededRunAsync(DestinationA);
        var lease = await StartAsync(Manual(DestinationA));

        var claimed = await _log.ClaimPruneAsync(lease, DestinationA, 2);

        claimed.Select(r => r.Id).ShouldBe([a2, a1]);
        claimed.ShouldAllBe(r => r.PrunedAt == _clock.GetUtcNow());
        (await _log.UnprunedSuccessesAsync(DestinationA)).Select(r => r.Id).ShouldBe([a4, a3]);
        (await _log.UnprunedSuccessesAsync(DestinationB)).Select(r => r.Id).ShouldBe([b1]);
        (await _log.ClaimPruneAsync(lease, DestinationA, 2)).ShouldBeEmpty();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _log.ClaimPruneAsync(lease, DestinationA, 0));
    }

    [Test]
    public async Task TryStart_WhenRunningRowLockFree_ShouldMarkAbandonedAndStart()
    {
        var dead = Guid.NewGuid();
        await using (var connection = await _database.OpenAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO backup_run (id, trigger, state, started_at, host, destination_id) VALUES (@dead, 'manual', 'running', @startedAt, 'gone', @destination)",
                new { dead, startedAt = Start.AddHours(-3), destination = DestinationA });
        }
        _clock.Advance(TimeSpan.FromMinutes(1));

        var lease = await StartAsync(Manual());

        var runs = await _log.RecentAsync(10);
        runs.Select(r => (r.Id, r.State)).ShouldBe([(lease.Run.Id, BackupRunState.Running), (dead, BackupRunState.Failed)]);
        var abandoned = runs[1];
        abandoned.Error.ShouldBe(BackupRunLog.AbandonedError);
        abandoned.Error.ShouldBe(PinnedFiles.Read("backup-run.json")["abandoned"]!["error"]!.GetValue<string>());
        abandoned.FinishedAt.ShouldBe(_clock.GetUtcNow());
        (await _log.SucceedAsync(dead, Outcome())).ShouldBeFalse();
    }

    // Two instances start at once: the lock lets exactly one through.
    [Test]
    public async Task TryStart_WhenTwoConnectionsRace_ShouldStartOne()
    {
        var instances = Enumerable.Range(0, 4).Select(_ => NewInstance()).ToList();

        var results = await Task.WhenAll(instances.Select(i => i.TryStartAsync(Manual())));
        _leases.AddRange(results.Select(r => r.Lease).OfType<BackupLease>());

        results.Count(r => r.Lease is not null).ShouldBe(1);
        results.Where(r => r.Lease is null).ShouldAllBe(r => r.Refusal == BackupRefusal.Busy);
        (await _log.RecentAsync(10)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Dispose_WhenLeaseReleased_ShouldFreeLock()
    {
        var lease = await StartAsync(Manual());
        (await HeldLocksAsync()).ShouldBe(1);

        await lease.DisposeAsync();

        (await HeldLocksAsync()).ShouldBe(0);
        var again = await NewInstance().TryStartAsync(Manual());
        _leases.Add(again.Lease.ShouldNotBeNull());
    }

    [Test]
    public async Task Succeed_WhenCalled_ShouldRecordOutcome()
    {
        var lease = await StartAsync(Manual());
        _clock.Advance(TimeSpan.FromSeconds(47));
        var outcome = new BackupOutcome("shiplean/database/shiplean-20261024T140000Z-a41d0e7c-g1.dump.slbk", 48_213_504, 12, 3_145_728);

        (await _log.SucceedAsync(lease.Run.Id, outcome)).ShouldBeTrue();

        var run = (await _log.LastSuccessAsync()).ShouldNotBeNull();
        run.ShouldBe(lease.Run with
        {
            State = BackupRunState.Succeeded, FinishedAt = Start.AddSeconds(47), DatabaseObjectKey = outcome.DatabaseObjectKey, DatabaseBytes = outcome.DatabaseBytes,
            AttachmentsCopied = outcome.AttachmentsCopied, AttachmentsBytes = outcome.AttachmentsBytes,
        });
        (await _log.SucceedAsync(lease.Run.Id, outcome)).ShouldBeFalse();
        (await _log.FailAsync(lease.Run.Id, "late")).ShouldBeFalse();
    }

    [Test]
    public async Task Fail_WhenCalled_ShouldRecordError()
    {
        var lease = await StartAsync(Manual());
        _clock.Advance(TimeSpan.FromSeconds(9));

        (await _log.FailAsync(lease.Run.Id, "The bucket refused the upload: Access Denied (403).")).ShouldBeTrue();

        var run = (await _log.RecentAsync(1)).Single();
        run.State.ShouldBe(BackupRunState.Failed);
        run.Error.ShouldBe("The bucket refused the upload: Access Denied (403).");
        run.FinishedAt.ShouldBe(Start.AddSeconds(9));
        (await _log.LastSuccessAsync()).ShouldBeNull();
    }

    [Test]
    public async Task UnprunedSuccesses_WhenSomePruned_ShouldListOthersNewestFirst()
    {
        var a1 = await SucceededRunAsync(DestinationA);
        var a2 = await SucceededRunAsync(DestinationA);
        await FailedRunAsync(DestinationA);
        var a3 = await SucceededRunAsync(DestinationA);
        var lease = await StartAsync(Manual(DestinationA));
        (await _log.ClaimPruneAsync(lease, DestinationA, 2)).Select(r => r.Id).ShouldBe([a1]);
        await lease.DisposeAsync();

        var unpruned = await _log.UnprunedSuccessesAsync(DestinationA);

        unpruned.Select(r => r.Id).ShouldBe([a3, a2]);
        unpruned.ShouldAllBe(r => r.State == BackupRunState.Succeeded && r.PrunedAt == null && r.DestinationId == DestinationA);
    }

    [Test]
    public async Task Recent_WhenManyRuns_ShouldReturnNewestFirst()
    {
        var first = await SucceededRunAsync(DestinationA);
        var second = await FailedRunAsync(DestinationA);
        await _log.RecordSkippedAsync(_clock.GetUtcNow());
        _clock.Advance(TimeSpan.FromMinutes(1));
        var third = await SucceededRunAsync(DestinationA);
        var fourth = await FailedRunAsync(DestinationA);

        (await _log.RecentAsync(3)).Select(r => r.Id).ShouldBe([fourth, third, second]);
        (await _log.RecentAsync(20)).Select(r => r.Id).ShouldBe([fourth, third, second, first]);
        (await _log.LastSuccessAsync()).ShouldNotBeNull().Id.ShouldBe(third);
    }

    [Test]
    public void LeaseConnectionString_WhenBuilt_ShouldTurnPoolingOffAndPinUtc()
    {
        var built = new NpgsqlConnectionStringBuilder(BackupRunLogServices.LeaseConnectionString(_database.ConnectionString));

        built.Pooling.ShouldBeFalse();
        built.Timezone.ShouldBe("UTC");
        built.Options.ShouldBe("-c idle_session_timeout=60000");
    }

    private BackupRunLog NewInstance() => new(_dataSource, BackupRunLogServices.LeaseConnectionString(_database.ConnectionString), _clock);

    private static BackupStart Manual(string destinationId = DestinationA) => new(BackupTriggerKind.Manual, null, null, 1, destinationId);

    private static BackupStart Scheduled(DateTimeOffset occurrence) => new(BackupTriggerKind.Scheduled, null, occurrence, 1, DestinationA);

    private static BackupOutcome Outcome() => new("shiplean/database/fake.dump.slbk", 1024, 0, 0);

    private static BackupSettingsRow Settings(DateTimeOffset updatedAt, int generation) => new(
        Enabled: true,
        Provider: "s3",
        Destination: """{"bucket": "shiplean-backups", "region": "eu-west-1"}""",
        DestinationSecret: "protected-secret",
        SecretLast4: "WXYZ",
        Cron: "0 2 * * *",
        KeepCount: 14,
        IncludeAttachments: true,
        Passphrase: "protected-passphrase",
        PassphraseGeneration: generation,
        PassphraseSetAt: updatedAt,
        UpdatedAt: updatedAt,
        UpdatedBy: null);

    private async Task<BackupLease> StartAsync(BackupStart start)
    {
        var result = await _log.TryStartAsync(start);
        var lease = result.Lease.ShouldNotBeNull();
        result.Refusal.ShouldBeNull();
        _leases.Add(lease);
        return lease;
    }

    private async Task<Guid> SucceededRunAsync(string destinationId)
    {
        var lease = await StartAsync(Manual(destinationId));
        (await _log.SucceedAsync(lease.Run.Id, Outcome())).ShouldBeTrue();
        await lease.DisposeAsync();
        _clock.Advance(TimeSpan.FromMinutes(1));
        return lease.Run.Id;
    }

    private async Task<Guid> FailedRunAsync(string destinationId)
    {
        var lease = await StartAsync(Manual(destinationId));
        (await _log.FailAsync(lease.Run.Id, "failed")).ShouldBeTrue();
        await lease.DisposeAsync();
        _clock.Advance(TimeSpan.FromMinutes(1));
        return lease.Run.Id;
    }

    private async Task<int> HeldLocksAsync()
    {
        await using var connection = await _database.OpenAsync();
        return await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*)::int FROM pg_locks
            WHERE locktype = 'advisory' AND classid = @ns AND objid = 1 AND objsubid = 2 AND granted
              AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
            """,
            new { ns = LOCK_NAMESPACE });
    }

    // Kills the session that holds the lease's lock, as a dead instance or a dropped network would.
    private async Task TerminateLeaseConnectionAsync()
    {
        await using var connection = await _database.OpenAsync();
        var pid = await connection.QuerySingleAsync<int>(
            """
            SELECT pid FROM pg_locks
            WHERE locktype = 'advisory' AND classid = @ns AND objid = 1 AND objsubid = 2 AND granted
              AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
            """,
            new { ns = LOCK_NAMESPACE });
        (await connection.ExecuteScalarAsync<bool>("SELECT pg_terminate_backend(@pid, 10000)", new { pid })).ShouldBeTrue();
    }
}
