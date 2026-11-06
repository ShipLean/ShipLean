using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class BackupConfigReaderServices : IServiceModule
{
    // TODO: the fixed reader (nothing saved) stands in for the reader of the saved settings, which replaces it here.
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBackupConfigReader>(new FixedBackupConfigReader(null));
}
