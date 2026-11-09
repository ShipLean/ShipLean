using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Job;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

// The backup job: dump, upload, mirror, record, prune.
public sealed class BackupJobServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBackupJob, BackupJob>();
}
