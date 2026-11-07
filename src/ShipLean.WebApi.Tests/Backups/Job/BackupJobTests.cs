using System.Diagnostics.Metrics;
using System.Text;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Job;
using ShipLean.Application.Features.Backups.Modules;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Contracts.Backups;
using ShipLean.Contracts.Usage;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport.Backups;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Import.Jira;

namespace ShipLean.WebApi.Tests.Backups.Job;

// The backup job (contract C-RUN, decisions D3-D6, D8, D9) over the engine fakes, InMemoryBackupDestination and the real
// run log on a database of its own, because the lease is one lock per database.
[TestFixture]
public class BackupJobTests
{
    // BackupLeaseLock's two-int key: pg_locks shows it as classid / objid with objsubid 2.
    private const int LOCK_NAMESPACE = 0x42_4B_55_50;
    private const string Passphrase = "correct horse battery staple";
    private const string Prefix = "shiplean/";
    private const int Generation = 3;

    private static readonly DateTimeOffset Start = new(2026, 10, 24, 14, 0, 0, TimeSpan.Zero);

    private readonly List<BackupLease> _leases = [];
    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private FakeTimeProvider _clock = null!;
    private BackupRunLog _log = null!;
    private ClaimFailingRunLog _runLog = null!;
    private InMemoryBackupDestination _destination = null!;
    private ScriptedDumper _dumper = null!;
    private ScriptedMirror _mirror = null!;
    private RecordingUsageEvents _usage = null!;
    private RecordingLogger<BackupJob> _logger = null!;
    private ServiceProvider _services = null!;
    private IMeterFactory _meters = null!;
    private DirectoryInfo _temp = null!;
    private BackupJob _job = null!;

    [SetUp]
    public async Task CloneDatabase()
    {
        _database = await ShipLeanDatabase.CloneEmptyAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        _clock = new FakeTimeProvider(Start);
        _log = new BackupRunLog(_dataSource, BackupRunLogServices.LeaseConnectionString(_database.ConnectionString), _clock);
        _runLog = new ClaimFailingRunLog(_log);
        _destination = new InMemoryBackupDestination();
        _dumper = new ScriptedDumper();
        _mirror = new ScriptedMirror();
        _usage = new RecordingUsageEvents();
        _logger = new RecordingLogger<BackupJob>();
        _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        _meters = _services.GetRequiredService<IMeterFactory>();
        _temp = Directory.CreateTempSubdirectory("shiplean-job-tests-");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [BackupTempFiles.TempKey] = _temp.FullName })
            .Build();
        var engine = new BackupJobEngine(new FakeBackupDestinationFactory(_destination), _dumper, new PassThroughBackupCipher(), _mirror);
        _job = new BackupJob(_runLog, engine, new BackupTempFiles(configuration), new BackupJobMetrics(_meters), _usage, _clock, _logger);
    }

    [TearDown]
    public async Task DropDatabase()
    {
        foreach (var lease in _leases)
        {
            await lease.DisposeAsync();
        }
        _leases.Clear();
        await _services.DisposeAsync();
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
        _temp.Delete(recursive: true);
    }

    [Test]
    public async Task Run_WhenConfigured_ShouldUploadEncryptedDumpAndSucceed()
    {
        var run = await RunAsync(Config());

        var key = BackupObjectKeys.Database(Prefix, run.StartedAt, run.Id, Generation);
        run.State.ShouldBe(BackupRunState.Succeeded);
        run.DatabaseObjectKey.ShouldBe(key);
        key.ShouldStartWith("shiplean/database/shiplean-20261024T140000Z-");
        key.ShouldEndWith("-g3.dump.slbk");
        (await KeysAsync()).ShouldBe([key]);
        var stored = _destination.Read(key);
        run.DatabaseBytes.ShouldBe(stored.Length);
        run.AttachmentsCopied.ShouldBe(0);
        run.AttachmentsBytes.ShouldBe(0);
        run.Error.ShouldBeNull();
        (await DecryptAsync(stored, Passphrase)).ShouldBe(FakeDatabaseDumper.Content);
        await Should.ThrowAsync<BackupDecryptException>(() => DecryptAsync(stored, "wrong passphrase"));
    }

    [Test]
    public async Task Run_WhenAttachmentsOn_ShouldMirrorAndRecordCopies()
    {
        _mirror.Result = new MirrorResult(4, 4096);

        var run = await RunAsync(Config(includeAttachments: true));

        _mirror.Calls.ShouldHaveSingleItem().ShouldBe((_destination, Prefix, Generation, Passphrase));
        run.State.ShouldBe(BackupRunState.Succeeded);
        run.AttachmentsCopied.ShouldBe(4);
        run.AttachmentsBytes.ShouldBe(4096);
    }

    [Test]
    public async Task Run_WhenOverKeepCount_ShouldDeleteOldestAndMarkPruned()
    {
        var config = Config(keepCount: 2);
        var first = await RunAsync(config);
        var second = await RunAsync(config);
        (await KeysAsync()).Count.ShouldBe(2);

        var third = await RunAsync(config);

        (await KeysAsync()).ShouldBe(new[] { second.DatabaseObjectKey!, third.DatabaseObjectKey! }.Order(StringComparer.Ordinal));
        (await _log.UnprunedSuccessesAsync(config.DestinationId)).Select(r => r.Id).ShouldBe([third.Id, second.Id]);
        var pruned = await RowAsync(first.Id);
        pruned.PrunedAt.ShouldBe(Start.AddMinutes(2));
        pruned.State.ShouldBe(BackupRunState.Succeeded);
    }

    [Test]
    public async Task Run_WhenDeleteFails_ShouldSucceedAndWarnWithKey()
    {
        var config = Config(keepCount: 1);
        var first = await RunAsync(config);
        _destination.Fail(BackupProbeResult.DeleteStep);

        var second = await RunAsync(config);

        second.State.ShouldBe(BackupRunState.Succeeded);
        (await RowAsync(first.Id)).PrunedAt.ShouldNotBeNull();
        (await KeysAsync()).ShouldContain(first.DatabaseObjectKey!);
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains(first.DatabaseObjectKey!, StringComparison.Ordinal));
    }

    [Test]
    public async Task Run_WhenUploadFails_ShouldFailAndNotPrune()
    {
        var config = Config(keepCount: 1, includeAttachments: true);
        var first = await RunAsync(config);
        _destination.Fail(BackupProbeResult.WriteStep);

        var second = await RunAsync(config);

        second.State.ShouldBe(BackupRunState.Failed);
        second.Error.ShouldNotBeNull().ShouldStartWith("The upload failed: The in-memory destination was told to fail at write");
        (await _log.UnprunedSuccessesAsync(config.DestinationId)).Select(r => r.Id).ShouldBe([first.Id]);
        (await KeysAsync()).ShouldBe([first.DatabaseObjectKey!]);
        _mirror.Calls.Count.ShouldBe(1);
    }

    [Test]
    public async Task Run_WhenDumpFails_ShouldFailWithMessage()
    {
        _dumper.AfterWrite = (_, _) => throw new DatabaseDumpException("pg_dump exited with code 1: could not connect to the server.");

        var run = await RunAsync(Config(includeAttachments: true));

        run.State.ShouldBe(BackupRunState.Failed);
        run.Error.ShouldBe("The database dump failed: pg_dump exited with code 1: could not connect to the server.");
        run.DatabaseObjectKey.ShouldBeNull();
        (await KeysAsync()).ShouldBeEmpty();
        _mirror.Calls.ShouldBeEmpty();
    }

    [Test]
    public async Task Run_WhenUnexpectedError_ShouldFailWithoutItsMessage()
    {
        _dumper.AfterWrite = (_, _) => throw new InvalidOperationException($"Host=db;Password=hunter2;Passphrase={Passphrase}");

        var run = await RunAsync(Config());

        run.State.ShouldBe(BackupRunState.Failed);
        run.Error.ShouldBe("The database dump failed (InvalidOperationException).");
    }

    [Test]
    public async Task Run_WhenAttachmentsOff_ShouldNotMirror()
    {
        var run = await RunAsync(Config(includeAttachments: false));

        run.State.ShouldBe(BackupRunState.Succeeded);
        _mirror.Calls.ShouldBeEmpty();
    }

    [Test]
    public async Task Run_WhenAttachmentsInDatabase_ShouldNotMirror()
    {
        var run = await RunAsync(Config(includeAttachments: true, attachmentsInDatabase: true));

        run.State.ShouldBe(BackupRunState.Succeeded);
        _mirror.Calls.ShouldBeEmpty();
    }

    [TestCase(null)]
    [TestCase(BackupProbeResult.WriteStep)]
    [TestCase("dump")]
    public async Task Run_WhenFinished_ShouldLeaveNoTempFile(string? failAt)
    {
        var tempFilesDuringDump = -1;
        _dumper.AfterWrite = (_, _) =>
        {
            tempFilesDuringDump = _temp.GetFiles().Length;
            return failAt == "dump" ? throw new DatabaseDumpException("pg_dump was killed.") : Task.CompletedTask;
        };
        if (failAt == BackupProbeResult.WriteStep)
        {
            _destination.Fail(BackupProbeResult.WriteStep);
        }

        var run = await RunAsync(Config());

        run.State.ShouldBe(failAt is null ? BackupRunState.Succeeded : BackupRunState.Failed);
        tempFilesDuringDump.ShouldBe(1);
        _temp.GetFiles().ShouldBeEmpty();
    }

    [Test]
    public async Task Run_WhenLeaseLost_ShouldStopAndFail()
    {
        var config = Config(keepCount: 1, includeAttachments: true);
        var first = await RunAsync(config);
        _dumper.AfterWrite = async (_, token) =>
        {
            await TerminateLeaseConnectionAsync();
            _clock.Advance(BackupLease.CheckInterval);
            await CancellationOfAsync(token);
        };

        var second = await RunAsync(config);

        second.State.ShouldBe(BackupRunState.Failed);
        second.Error.ShouldBe(BackupJob.LeaseLostError);
        (await KeysAsync()).ShouldBe([first.DatabaseObjectKey!]);
        _mirror.Calls.Count.ShouldBe(1);
        (await _log.UnprunedSuccessesAsync(config.DestinationId)).Select(r => r.Id).ShouldBe([first.Id]);
    }

    [Test]
    public async Task Run_WhenPruneFails_ShouldStaySucceeded()
    {
        var config = Config(keepCount: 1);
        var first = await RunAsync(config);
        _runLog.FailClaims = true;

        var second = await RunAsync(config);

        second.State.ShouldBe(BackupRunState.Succeeded);
        (await _log.UnprunedSuccessesAsync(config.DestinationId)).Select(r => r.Id).ShouldBe([second.Id, first.Id]);
        (await KeysAsync()).Count.ShouldBe(2);
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("no older backups were pruned", StringComparison.Ordinal));
    }

    [Test]
    public async Task Prune_WhenOtherDestinationRuns_ShouldNotTouchThem()
    {
        var other = Config(prefix: "elsewhere/");
        var otherFirst = await RunAsync(other);
        var otherSecond = await RunAsync(other);
        var config = Config(keepCount: 1);
        await RunAsync(config);
        await RunAsync(config);

        var last = await RunAsync(config);

        other.DestinationId.ShouldNotBe(config.DestinationId);
        (await _log.UnprunedSuccessesAsync(config.DestinationId)).Select(r => r.Id).ShouldBe([last.Id]);
        (await _log.UnprunedSuccessesAsync(other.DestinationId)).Select(r => r.Id).ShouldBe([otherSecond.Id, otherFirst.Id]);
        (await KeysAsync()).ShouldBe(new[] { otherFirst.DatabaseObjectKey!, otherSecond.DatabaseObjectKey!, last.DatabaseObjectKey! }.Order(StringComparer.Ordinal));
    }

    [Test]
    public async Task Prune_WhenLeaseLostBeforeClaim_ShouldDeleteNothing()
    {
        var config = Config(keepCount: 1, includeAttachments: true);
        var first = await RunAsync(config);
        // The lock goes after the upload and before the claim; the loss check hasn't run yet, so only the claim can see it.
        _mirror.Step = TerminateLeaseConnectionAsync;

        var second = await RunAsync(config);

        second.State.ShouldBe(BackupRunState.Succeeded);
        (await _log.UnprunedSuccessesAsync(config.DestinationId)).Select(r => r.Id).ShouldBe([second.Id, first.Id]);
        (await KeysAsync()).Count.ShouldBe(2);
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("no older backups were pruned", StringComparison.Ordinal));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Run_WhenFinished_ShouldEmitRunFinished(bool succeeds)
    {
        _dumper.AfterWrite = (_, _) =>
        {
            _clock.Advance(TimeSpan.FromSeconds(42));
            return succeeds ? Task.CompletedTask : throw new DatabaseDumpException("pg_dump failed.");
        };

        await RunAsync(Config());

        var finished = _usage.Events.ShouldHaveSingleItem();
        finished.Name.ShouldBe(UsageEventNames.BackupRunFinished);
        finished.Surface.ShouldBe(UsageSurface.System);
        finished.Actor.ShouldBe(UsageActor.System);
        finished.Outcome.ShouldBe(succeeds ? UsageOutcome.Ok : UsageOutcome.Error);
        finished.DurationMs.ShouldBe(42_000);
        finished.Source.ShouldBe("system:BackupJob");
        finished.Properties.ShouldBe(new Dictionary<string, object> { [UsageProps.Provider] = "s3" });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Run_WhenFinished_ShouldCountRunByOutcome(bool succeeds)
    {
        if (!succeeds)
        {
            _destination.Fail(BackupProbeResult.WriteStep);
        }
        var counted = new List<(long Value, object? Outcome)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Scope == _meters && instrument.Meter.Name == BackupJobMetrics.MeterName && instrument.Name == BackupJobMetrics.RunsInstrument)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) => counted.Add((value, tags.ToArray().Single(t => t.Key == "outcome").Value)));
        listener.Start();

        await RunAsync(Config());

        counted.ShouldBe([(1L, (object?)(succeeds ? "succeeded" : "failed"))]);
    }

    private static BackupConfig Config(string prefix = Prefix, int keepCount = 14, bool includeAttachments = false, bool attachmentsInDatabase = false)
    {
        var destination = new BackupDestinationConfig("s3", new S3DestinationConfig("eu-west-1", null, "shiplean-backups", prefix, false, "AKIAEXAMPLE", "secret-key-value"));
        return new BackupConfig(
            true, destination, BackupDestinationId.Of(destination), "0 2 * * *", keepCount, includeAttachments, attachmentsInDatabase, Passphrase, Generation, Start);
    }

    private static async Task<string> DecryptAsync(byte[] stored, string passphrase)
    {
        using var plain = new MemoryStream();
        await new PassThroughBackupCipher().DecryptAsync(new MemoryStream(stored), plain, passphrase, CancellationToken.None);
        return Encoding.ASCII.GetString(plain.ToArray());
    }

    // Waits until the job's token is cancelled, then throws as a cancelled step does; fails after 30 s instead of hanging.
    private static async Task CancellationOfAsync(CancellationToken token)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var onCancel = token.Register(() => cancelled.TrySetCanceled(token));
        await using var onTimeout = timeout.Token.Register(() => cancelled.TrySetException(new TimeoutException("The job's token was never cancelled.")));
        await cancelled.Task;
    }

    // Starts a manual run as the trigger would, hands it to the job, then releases the lease.
    private async Task<BackupRunRow> RunAsync(BackupConfig config)
    {
        var started = await _log.TryStartAsync(new BackupStart(BackupTriggerKind.Manual, null, null, config.PassphraseGeneration, config.DestinationId));
        var lease = started.Lease.ShouldNotBeNull();
        _leases.Add(lease);

        await _job.RunAsync(lease, config);

        await lease.DisposeAsync();
        _clock.Advance(TimeSpan.FromMinutes(1));
        return await RowAsync(lease.Run.Id);
    }

    private async Task<BackupRunRow> RowAsync(Guid id) => (await _log.RecentAsync(100)).Single(r => r.Id == id);

    private async Task<IReadOnlyList<string>> KeysAsync()
    {
        List<string> keys = [];
        await foreach (var stored in _destination.ListAsync(string.Empty, CancellationToken.None))
        {
            keys.Add(stored.Key);
        }
        return keys;
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
