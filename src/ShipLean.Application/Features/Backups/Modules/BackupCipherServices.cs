using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Contracts;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class BackupCipherServices : IServiceModule
{
    // TODO: the pass-through cipher stands in for the SLBK1 AES-GCM cipher, which replaces it here.
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBackupCipher>(new PassThroughBackupCipher());
}
