using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Dump;
using ShipLean.Contracts;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class DatabaseDumperServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IDatabaseDumper>(new PgDumpDatabaseDumper(configuration));
}
