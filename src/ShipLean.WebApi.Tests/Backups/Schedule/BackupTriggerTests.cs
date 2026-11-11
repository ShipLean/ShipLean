using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Application.Features.Backups.Schedule;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// BackupTrigger and BackupWorker over the real run log (contract C-RUN): Back up now reads the config once, takes the
// lease, returns the Running row and leaves the job to the worker, which always releases the lease.
[TestFixture]
public class BackupTriggerTests : BackupScheduleFixture
{
    private static readonly Guid Admin = SeedIds.Users.Joel;

    protected override bool Seeded => true;

    [Test]
    public async Task Request_WhenRunning_ShouldThrowConflict()
    {
        var instance = NewInstance();
        await instance.StartWorkerAsync();
        Probe.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = await instance.Trigger.RequestAsync(BackupTriggerKind.Manual, Admin, null);
        await Probe.NextStartAsync();

        var refused = await Should.ThrowAsync<ApiException>(() => NewInstance().Trigger.RequestAsync(BackupTriggerKind.Manual, Admin, null));

        refused.Code.ShouldBe(ApiErrorCodes.Conflict);
        refused.Message.ShouldBe("A backup is already running.");
        Probe.Gate.SetResult();
        (await Probe.NextEndAsync()).ShouldBe(first.Run!.Id);
        (await RowsAsync()).Count.ShouldBe(1);
    }

    [Test]
    public async Task Request_WhenNotConfigured_ShouldThrowNotConfigured()
    {
        Configs.Config = null;
        var instance = NewInstance();
        await instance.StartWorkerAsync();

        var refused = await Should.ThrowAsync<ApiException>(() => instance.Trigger.RequestAsync(BackupTriggerKind.Manual, Admin, null));

        refused.Code.ShouldBe(ApiErrorCodes.NotConfigured);
        (await RowsAsync()).ShouldBeEmpty();
        // No lease was left behind.
        await using var lease = (await instance.RunLog.TryStartAsync(new BackupStart(BackupTriggerKind.Manual, null, null, 1, "any"))).Lease.ShouldNotBeNull();
    }

    [Test]
    public async Task Request_WhenAccepted_ShouldRunJobOnceAndReleaseLease()
    {
        var instance = NewInstance();
        await instance.StartWorkerAsync();
        var config = Configs.Config!;

        var result = await instance.Trigger.RequestAsync(BackupTriggerKind.Manual, Admin, null);

        var run = result.Run.ShouldNotBeNull();
        result.Refusal.ShouldBeNull();
        run.State.ShouldBe(BackupRunState.Running);
        run.Trigger.ShouldBe(BackupTriggerKind.Manual);
        run.StartedBy.ShouldBe(Admin);
        run.PassphraseGeneration.ShouldBe(config.PassphraseGeneration);
        run.DestinationId.ShouldBe(config.DestinationId);
        var (startedRun, snapshot) = await Probe.NextStartAsync();
        startedRun.Id.ShouldBe(run.Id);
        snapshot.ShouldBeSameAs(config);
        (await Probe.NextEndAsync()).ShouldBe(run.Id);
        Probe.StartCount.ShouldBe(1);
        (await RowsAsync()).Single().State.ShouldBe("succeeded");
        await using var next = (await instance.RunLog.TryStartAsync(new BackupStart(BackupTriggerKind.Manual, null, null, 1, "any"))).Lease.ShouldNotBeNull();
        Probe.StartCount.ShouldBe(1);
    }

    [Test]
    public async Task Worker_WhenJobThrows_ShouldReleaseLease()
    {
        var instance = NewInstance();
        await instance.StartWorkerAsync();
        Probe.Throw = true;

        var run = (await instance.Trigger.RequestAsync(BackupTriggerKind.Manual, Admin, null)).Run!;

        (await Probe.NextEndAsync()).ShouldBe(run.Id);
        var row = (await RowsAsync()).Single();
        row.State.ShouldBe("failed");
        row.Error.ShouldBe(BackupWorker.JobThrewError);
        await using var next = (await instance.RunLog.TryStartAsync(new BackupStart(BackupTriggerKind.Manual, null, null, 1, "any"))).Lease.ShouldNotBeNull();
    }

    [Test]
    public async Task Request_WhenSettingsChangeDuringRun_ShouldKeepSnapshot()
    {
        var instance = NewInstance();
        await instance.StartWorkerAsync();
        var original = Configs.Config!;
        Probe.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = (await instance.Trigger.RequestAsync(BackupTriggerKind.Manual, Admin, null)).Run!;
        var (_, snapshot) = await Probe.NextStartAsync();
        Configs.Config = Config(bucket: "another-bucket", passphrase: "a brand new passphrase for g2", generation: 2, updatedAt: Start.AddMinutes(1));
        Probe.Gate.SetResult();
        await Probe.NextEndAsync();

        Configs.Reads.ShouldBe(1);
        snapshot.ShouldBe(original);
        snapshot.Passphrase.ShouldBe("correct horse battery staple");
        snapshot.Destination.S3!.Bucket.ShouldBe("shiplean-backups");
        var row = (await RowsAsync()).Single();
        row.PassphraseGeneration.ShouldBe(1);
        row.DestinationId.ShouldBe(original.DestinationId);
        row.DestinationId.ShouldNotBe(Configs.Config.DestinationId);
        run.PassphraseGeneration.ShouldBe(1);
    }
}
