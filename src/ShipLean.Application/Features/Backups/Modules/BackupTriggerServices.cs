using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Application.Features.Backups.Schedule;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

// The backup trigger, the queue to its worker, the worker and the cron schedule. Test hosts remove the schedule
// (HostProfiles) and keep the worker.
public sealed class BackupTriggerServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<BackupQueue>();
        services.AddSingleton<IBackupTrigger, BackupTrigger>();
        services.AddHostedService<BackupWorker>();
        services.AddHostedService<BackupScheduleService>();
    }
}
