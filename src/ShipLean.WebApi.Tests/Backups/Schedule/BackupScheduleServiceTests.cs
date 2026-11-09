using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Application.Features.Backups.Schedule;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// BackupScheduleService on a fake clock over the real run log (decisions D2 and D8). The saved config is hourly at :00,
// saved at 14:00, so the first occurrence is 15:00. Each test waits for the service's sleeps (WatchedClock) rather than
// for time to pass: a sleep starts only when a tick has finished.
[TestFixture]
public class BackupScheduleServiceTests : BackupScheduleFixture
{
    private static readonly DateTimeOffset Three = Start.AddHours(1);
    private static readonly DateTimeOffset Four = Start.AddHours(2);

    [Test]
    public async Task Schedule_WhenDue_ShouldStartOneScheduledRun()
    {
        var instance = await StartedAsync();
        (await instance.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.PollInterval);

        Time.Advance(TimeSpan.FromHours(1));

        var (run, _) = await Probe.NextStartAsync();
        run.Trigger.ShouldBe(BackupTriggerKind.Scheduled);
        run.ScheduledFor.ShouldBe(Three);
        (await instance.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.PollInterval);
        await Probe.NextEndAsync();
        Time.Advance(BackupScheduleService.PollInterval);
        (await instance.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.PollInterval);
        Probe.StartCount.ShouldBe(1);
        var row = (await RowsAsync()).ShouldHaveSingleItem();
        row.State.ShouldBe("succeeded");
        row.ScheduledFor.ShouldBe(Three);
    }

    [Test]
    public async Task Schedule_WhenTwoServicesShareDatabase_ShouldStartOccurrenceOnce()
    {
        var first = await StartedAsync();
        var second = await StartedAsync();
        await first.Clock.NextSleepAsync();
        await second.Clock.NextSleepAsync();

        Time.Advance(TimeSpan.FromHours(1));

        await Probe.NextStartAsync();
        await Probe.NextEndAsync();
        // The one that lost either saw the occurrence taken, or found the lease held and retries in 5 minutes.
        var sleeps = new[] { await first.Clock.NextSleepAsync(), await second.Clock.NextSleepAsync() };
        if (sleeps.Contains(BackupScheduleService.BusyRetry))
        {
            Time.Advance(BackupScheduleService.BusyRetry);
            var retried = sleeps[0] == BackupScheduleService.BusyRetry ? first : second;
            (await retried.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.PollInterval);
        }
        Probe.StartCount.ShouldBe(1);
        var row = (await RowsAsync()).ShouldHaveSingleItem();
        row.ScheduledFor.ShouldBe(Three);
        row.State.ShouldBe("succeeded");
    }

    [Test]
    public async Task Schedule_WhenDisabled_ShouldNotStart()
    {
        Configs.Config = Config(enabled: false);
        var instance = await StartedAsync();
        (await instance.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.PollInterval);

        Time.Advance(TimeSpan.FromHours(1));

        (await instance.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.PollInterval);
        Probe.StartCount.ShouldBe(0);
        (await RowsAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Schedule_WhenCronChanged_ShouldUseNewCron()
    {
        var instance = await StartedAsync();
        await instance.Clock.NextSleepAsync();
        Configs.Config = Config(cron: "30 * * * *", updatedAt: Start.AddMinutes(5));

        Time.Advance(TimeSpan.FromMinutes(30));

        var (run, snapshot) = await Probe.NextStartAsync();
        run.ScheduledFor.ShouldBe(Start.AddMinutes(30));
        snapshot.Cron.ShouldBe("30 * * * *");
        // Next is 15:30, an hour away: it sleeps the minute.
        (await instance.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.PollInterval);
    }

    [Test]
    public async Task Schedule_WhenBusyAtDue_ShouldRetryInFiveMinutes()
    {
        var instance = await StartedAsync();
        await instance.Clock.NextSleepAsync();
        var held = await HoldLeaseAsync(instance);
        Time.Advance(TimeSpan.FromHours(1));

        (await instance.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.BusyRetry);
        await held.DisposeAsync();
        Time.Advance(BackupScheduleService.BusyRetry - TimeSpan.FromSeconds(1));
        Probe.StartCount.ShouldBe(0);
        Time.Advance(TimeSpan.FromSeconds(1));

        var (run, _) = await Probe.NextStartAsync();
        run.ScheduledFor.ShouldBe(Three);
        run.StartedAt.ShouldBe(Three.AddMinutes(5));
    }

    [Test]
    public async Task Schedule_WhenBusyPastNextOccurrence_ShouldRecordSkippedAndAdvance()
    {
        var instance = await StartedAsync();
        await instance.Clock.NextSleepAsync();
        var held = await HoldLeaseAsync(instance);
        Time.Advance(TimeSpan.FromHours(1));
        (await instance.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.BusyRetry);

        // Still busy when 16:00 comes: 15:00 is skipped, and 16:00 is retried in turn.
        Time.Advance(TimeSpan.FromHours(1));

        (await instance.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.BusyRetry);
        (await instance.RunLog.LatestOccurrenceAsync()).ShouldBe(Three);
        var skipped = (await RowsAsync()).Single(r => r.Trigger == "scheduled");
        skipped.State.ShouldBe("skipped");
        skipped.ScheduledFor.ShouldBe(Three);
        await held.DisposeAsync();
        Time.Advance(BackupScheduleService.BusyRetry);
        var (run, _) = await Probe.NextStartAsync();
        run.ScheduledFor.ShouldBe(Four);
        Probe.StartCount.ShouldBe(1);
    }

    [Test]
    public async Task Schedule_WhenRestartedAfterSkip_ShouldTargetFollowingOccurrence()
    {
        var before = await StartedAsync();
        await before.Clock.NextSleepAsync();
        var held = await HoldLeaseAsync(before);
        Time.Advance(TimeSpan.FromHours(1));
        await before.Clock.NextSleepAsync();
        Time.Advance(TimeSpan.FromHours(1));
        await before.Clock.NextSleepAsync();
        await StopAsync(before);
        await held.DisposeAsync();

        var after = await StartedAsync();

        var (run, _) = await Probe.NextStartAsync();
        run.ScheduledFor.ShouldBe(Four);
        (await after.Clock.NextSleepAsync()).ShouldBe(BackupScheduleService.PollInterval);
        await Probe.NextEndAsync();
        Probe.StartCount.ShouldBe(1);
        var scheduled = (await RowsAsync()).Where(r => r.Trigger == "scheduled").Select(r => (r.ScheduledFor, r.State)).ToList();
        scheduled.ShouldBe([(Three, "skipped"), (Four, "succeeded")]);
    }

    private async Task<ScheduleInstance> StartedAsync()
    {
        var instance = NewInstance();
        await instance.StartWorkerAsync();
        await instance.StartScheduleAsync();
        return instance;
    }

    // Another run (a Back up now elsewhere) holds the lease until the test disposes it.
    private static async Task<BackupLease> HoldLeaseAsync(ScheduleInstance instance)
        => (await instance.RunLog.TryStartAsync(new BackupStart(BackupTriggerKind.Manual, null, null, 1, "elsewhere"))).Lease.ShouldNotBeNull();
}
