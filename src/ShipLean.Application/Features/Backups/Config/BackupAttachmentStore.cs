using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Backups.Dtos;

namespace ShipLean.Application.Features.Backups.Config;

// Where attachments live, as the Backups tab and the config reader see it. When the blob store is Postgres on the same
// host, port and database as SHIPLEAN_DB_CONNECTION, the database dump already holds every attachment (D5).
public static class BackupAttachmentStore
{
    public static BackupAttachmentStoreKind Kind(BlobStoreKind kind) => kind switch
    {
        BlobStoreKind.File => BackupAttachmentStoreKind.LocalDisk,
        BlobStoreKind.Postgres => BackupAttachmentStoreKind.Postgres,
        BlobStoreKind.S3 => BackupAttachmentStoreKind.S3,
        _ => throw new UnreachableException($"Blob store kind {kind} has no backup attachment store kind."),
    };

    public static bool InDatabase(BlobStoreChoice choice, IConfiguration configuration)
    {
        if (choice.Kind != BlobStoreKind.Postgres)
        {
            return false;
        }
        var blobs = new NpgsqlConnectionStringBuilder(configuration[BlobStoreServices.DbConnectionKey]);
        var app = new NpgsqlConnectionStringBuilder(configuration[DatabaseServices.ConnectionKey]);
        return string.Equals(blobs.Host, app.Host, StringComparison.OrdinalIgnoreCase)
               && blobs.Port == app.Port
               && string.Equals(blobs.Database, app.Database, StringComparison.Ordinal);
    }
}
