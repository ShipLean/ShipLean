using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.S3;
using ShipLean.Contracts;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class BackupDestinationFactoryServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBackupDestinationFactory, S3BackupDestinationFactory>();
}
