using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Backups.Runs;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

// The backup trigger.
public sealed class BackupTriggerServices : IServiceModule
{
    // TODO: FakeBackupTrigger stands in until the real trigger, its queue and worker, and the cron schedule replace it.
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBackupTrigger, FakeBackupTrigger>();
}
