using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ShipLean.Application.Storage;

// Which blob store the configuration asks for. There is no picker setting: the first connection setting present wins,
// S3 (SHIPLEAN_BLOB_S3_CONNECTION), then Postgres (SHIPLEAN_BLOB_DB_CONNECTION), else the folder at SHIPLEAN_BLOB_ROOT
// (or data/blobs under the content root). Description is safe to log: no password or secret key.
public sealed record BlobStoreChoice(BlobStoreKind Kind, string Setting, string Description, string? Warning)
{
    public static BlobStoreChoice From(IConfiguration configuration, string contentRootPath)
    {
        var s3 = Present(configuration[BlobStoreServices.S3ConnectionKey]);
        var db = Present(configuration[BlobStoreServices.DbConnectionKey]);
        if (s3 is not null)
        {
            var warning = db is null
                ? null
                : $"{BlobStoreServices.S3ConnectionKey} and {BlobStoreServices.DbConnectionKey} are both set; S3 wins and {BlobStoreServices.DbConnectionKey} is ignored.";
            return new BlobStoreChoice(BlobStoreKind.S3, BlobStoreServices.S3ConnectionKey, S3BlobConnection.Parse(s3).ToString(), warning);
        }

        if (db is not null)
        {
            var builder = new NpgsqlConnectionStringBuilder(db) { Password = null };
            return new BlobStoreChoice(BlobStoreKind.Postgres, BlobStoreServices.DbConnectionKey, builder.ConnectionString, null);
        }

        var root = Present(configuration[BlobStoreServices.RootKey])
                   ?? Path.Combine(contentRootPath, "data", "blobs");
        return new BlobStoreChoice(BlobStoreKind.File, BlobStoreServices.RootKey, Path.GetFullPath(root), null);
    }

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
