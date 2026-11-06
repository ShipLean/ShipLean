using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Contracts;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class DatabaseDumperServices : IServiceModule
{
    // TODO: the fake stands in for the pg_dump dumper, which replaces it here.
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IDatabaseDumper>(new FakeDatabaseDumper());
}
