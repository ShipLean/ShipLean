using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Database;
using ShipLean.Contracts;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Storage;

// The blob store is S3, Postgres or a folder, whichever connection setting is present (BlobStoreChoice). The choice is
// made when IBlobStore is first resolved, so test hosts that override configuration get their own.
public sealed class BlobStoreServices : IServiceModule
{
    public const string RootKey = "SHIPLEAN_BLOB_ROOT";
    public const string DbConnectionKey = "SHIPLEAN_BLOB_DB_CONNECTION";
    public const string S3ConnectionKey = "SHIPLEAN_BLOB_S3_CONNECTION";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(sp => BlobStoreChoice.From(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IHostEnvironment>().ContentRootPath));
        services.AddSingleton<IBlobStore>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var choice = sp.GetRequiredService<BlobStoreChoice>();
            return choice.Kind switch
            {
                BlobStoreKind.S3 => new S3BlobStore(S3BlobConnection.Parse(config[S3ConnectionKey]!)),
                BlobStoreKind.Postgres => new PostgresBlobStore(DatabaseServices.PinUtc(config[DbConnectionKey]!)),
                _ => new LocalDiskBlobStore(choice.Description),
            };
        });
        services.AddHostedService<BlobStoreStartup>();
    }
}
