using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Config;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class BackupConfigReaderServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBackupConfigReader, BackupConfigReader>();
}
