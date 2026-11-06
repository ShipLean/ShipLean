using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Contracts;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class BackupDestinationFactoryServices : IServiceModule
{
    // TODO: the fake stands in for the S3 destination factory, which replaces it here.
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBackupDestinationFactory>(new FakeBackupDestinationFactory());
}
