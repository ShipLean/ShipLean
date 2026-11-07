using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Admin;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class BackupAdminServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<IBackupAdmin, BackupAdmin>();
}
