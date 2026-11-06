using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Backups.Runs;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

// The backup job.
public sealed class BackupJobServices : IServiceModule
{
    // TODO: FakeBackupJob stands in until the real pipeline (dump, upload, mirror, prune) replaces it.
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBackupJob, FakeBackupJob>();
}
