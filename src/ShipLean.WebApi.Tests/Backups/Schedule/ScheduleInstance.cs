using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ShipLean.Application.Features.Backups.Cron;
using ShipLean.Application.Features.Backups.Modules;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Application.Features.Backups.Schedule;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// One app instance's backup pieces, built by hand over a shared database: its own run log (so its own lease connections),
// queue, trigger, worker and schedule, on its own WatchedClock. The worker runs a ProbeJob in a scope per run.
public sealed class ScheduleInstance : IAsyncDisposable
{
    private bool _workerStarted;
    private bool _scheduleStarted;

    public ScheduleInstance(NpgsqlDataSource dataSource, string connectionString, WatchedClock clock, SettableConfigReader configs, JobProbe probe)
    {
        Clock = clock;
        RunLog = new BackupRunLog(dataSource, BackupRunLogServices.LeaseConnectionString(connectionString), clock);
        var queue = new BackupQueue();
        Trigger = new BackupTrigger(configs, RunLog, queue);
        Worker = new BackupWorker(queue, new ProbeScopes(probe, RunLog), RunLog, NullLogger<BackupWorker>.Instance);
        Schedule = new BackupScheduleService(configs, RunLog, Trigger, new CronSchedule(clock), clock, NullLogger<BackupScheduleService>.Instance);
    }

    public WatchedClock Clock { get; }

    public BackupRunLog RunLog { get; }

    public BackupTrigger Trigger { get; }

    public BackupWorker Worker { get; }

    public BackupScheduleService Schedule { get; }

    public async Task StartWorkerAsync()
    {
        _workerStarted = true;
        await Worker.StartAsync(CancellationToken.None);
    }

    public async Task StartScheduleAsync()
    {
        _scheduleStarted = true;
        await Schedule.StartAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_scheduleStarted)
        {
            await Schedule.StopAsync(CancellationToken.None);
        }
        if (_workerStarted)
        {
            await Worker.StopAsync(CancellationToken.None);
        }
    }
}
