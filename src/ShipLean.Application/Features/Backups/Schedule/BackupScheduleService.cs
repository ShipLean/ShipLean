using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Features.Backups.Cron;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Runs;

namespace ShipLean.Application.Features.Backups.Schedule;

// The cron schedule (decisions D2 and D8). Every instance runs it; the occurrence index makes each occurrence start once.
// Each tick reads the saved config. When it is enabled, the next occurrence is due after the later of the schedule's
// cursor (IBackupRunLog.LatestOccurrenceAsync) and the last save, so a changed cron takes effect at once and a fresh
// schedule never runs occurrences from before it was saved. A due occurrence is requested through IBackupTrigger. Busy
// (another run holds the lease) retries the same occurrence every 5 minutes until the next one has come; it is then
// recorded as skipped, which moves the cursor on every instance. OccurrenceTaken (another instance has it) moves on.
// Otherwise it sleeps until the next occurrence, and at most a minute, so a save is noticed within a minute.
// Test hosts remove it (HostProfiles), so no test runs a backup it didn't ask for.
public sealed class BackupScheduleService(
    IBackupConfigReader configs,
    IBackupRunLog runLog,
    IBackupTrigger trigger,
    ICronSchedule cron,
    TimeProvider clock,
    ILogger<BackupScheduleService> logger) : IHostedService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan BusyRetry = TimeSpan.FromMinutes(5);

    private CancellationTokenSource? _stopping;
    private Task _loop = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stopping = new CancellationTokenSource();
        // Like the holiday job: run without the starter's execution context.
        using (ExecutionContext.SuppressFlow())
        {
            _loop = Task.Run(() => RunAsync(_stopping.Token), CancellationToken.None);
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopping, null) is not { } stopping)
        {
            return;
        }
        await stopping.CancelAsync();
        await Task.WhenAny(_loop, Task.Delay(Timeout.Infinite, cancellationToken));
        if (_loop.IsCompleted)
        {
            stopping.Dispose();
        }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private async Task RunAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            TimeSpan wait;
            try
            {
                wait = await TickAsync(stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception failed)
            {
                logger.LogError(failed, "The backup schedule failed; it tries again in a minute");
                wait = PollInterval;
            }
            try
            {
                await Task.Delay(wait, clock, stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
        }
    }

    // One look at the schedule: starts or skips what is due, and returns how long to sleep.
    private async Task<TimeSpan> TickAsync(CancellationToken stopping)
    {
        if (await configs.ReadAsync(stopping) is not { Enabled: true } config)
        {
            return PollInterval;
        }
        DateTimeOffset? requested = null;
        while (true)
        {
            var now = clock.GetUtcNow();
            var cursor = await runLog.LatestOccurrenceAsync(stopping) is { } latest && latest > config.UpdatedAt ? latest : config.UpdatedAt;
            if (cron.Next(config.Cron, cursor) is not { } due)
            {
                return PollInterval;
            }
            if (now < due)
            {
                return Min(due - now, PollInterval);
            }
            if (due == requested)
            {
                // Started or taken, yet the cursor hasn't reached it: look again on the next tick rather than spin.
                return PollInterval;
            }
            var next = cron.Next(config.Cron, due);
            if (next is { } following && now >= following)
            {
                var skipped = LastMissed(config.Cron, due, following, now);
                await runLog.RecordSkippedAsync(skipped, stopping);
                logger.LogWarning("Scheduled backups from {Due:O} to {Skipped:O} were skipped: another backup held the lease, or no instance was running", due, skipped);
                continue;
            }
            requested = due;
            var result = await trigger.RequestAsync(BackupTriggerKind.Scheduled, null, due, stopping);
            switch (result.Refusal)
            {
                case null:
                    logger.LogInformation("Scheduled backup {RunId} for {Due:O} started", result.Run?.Id, due);
                    break;
                case BackupRefusal.OccurrenceTaken:
                    break;
                case BackupRefusal.Busy:
                    return next is { } nextDue ? Min(BusyRetry, nextDue - now) : BusyRetry;
                default:
                    throw new InvalidOperationException($"Unknown backup refusal {result.Refusal}.");
            }
        }
    }

    // due and following have both come. Walks on to the newest occurrence that has come and returns the one before it:
    // recording that one as skipped makes the newest the next due, so a long outage skips in one row and then runs once.
    private DateTimeOffset LastMissed(string cronText, DateTimeOffset due, DateTimeOffset following, DateTimeOffset now)
    {
        var missed = due;
        var newest = following;
        while (cron.Next(cronText, newest) is { } after && after <= now)
        {
            missed = newest;
            newest = after;
        }
        return missed;
    }
}
