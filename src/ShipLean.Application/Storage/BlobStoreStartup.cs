using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Database;

namespace ShipLean.Application.Storage;

// At startup: logs which blob store was chosen and why (secrets redacted), and for Postgres applies db/blobs to the blob
// database when SHIPLEAN_DB_MIGRATE allows (it may be a database of its own, which db/migrations never reaches).
public sealed class BlobStoreStartup(BlobStoreChoice choice, IConfiguration configuration, ILogger<BlobStoreStartup> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Blob store: {Kind}, chosen by {Setting} ({Description})", choice.Kind, choice.Setting, choice.Description);
        if (choice.Warning is not null)
        {
            logger.LogWarning("{Warning}", choice.Warning);
        }

        if (choice.Kind == BlobStoreKind.Postgres && configuration.GetValue(DatabaseServices.MigrateKey, true))
        {
            PostgresBlobStore.ApplySchema(DatabaseServices.PinUtc(configuration[BlobStoreServices.DbConnectionKey]!));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
