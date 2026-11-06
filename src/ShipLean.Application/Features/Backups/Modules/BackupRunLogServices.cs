using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

// The backup run log. Its leases open their own unpooled connections from SHIPLEAN_DB_CONNECTION, read at resolve time.
public sealed class BackupRunLogServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBackupRunLog>(sp => new BackupRunLog(
            sp.GetRequiredService<NpgsqlDataSource>(),
            LeaseConnectionString(sp.GetRequiredService<IConfiguration>()[DatabaseServices.ConnectionKey]
                ?? throw new InvalidOperationException($"{DatabaseServices.ConnectionKey} is not set.")),
            sp.GetRequiredService<TimeProvider>()));

    // The app's connection string, pinned to UTC as every session is, with pooling off.
    public static string LeaseConnectionString(string connectionString)
        => new NpgsqlConnectionStringBuilder(DatabaseServices.PinUtc(connectionString)) { Pooling = false }.ConnectionString;
}
