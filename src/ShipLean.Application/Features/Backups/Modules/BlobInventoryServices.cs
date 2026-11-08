using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Storage;
using ShipLean.Application.Storage.Inventory;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

// The inventory over the store BlobStoreServices picked: the same BlobStoreChoice, the same connection settings.
public sealed class BlobInventoryServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBlobInventory>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var choice = sp.GetRequiredService<BlobStoreChoice>();
            return choice.Kind switch
            {
                BlobStoreKind.S3 => new S3BlobInventory(S3BlobConnection.Parse(config[BlobStoreServices.S3ConnectionKey]!)),
                BlobStoreKind.Postgres => new PostgresBlobInventory(DatabaseServices.PinUtc(config[BlobStoreServices.DbConnectionKey]!)),
                _ => new LocalDiskBlobInventory(choice.Description),
            };
        });
}
