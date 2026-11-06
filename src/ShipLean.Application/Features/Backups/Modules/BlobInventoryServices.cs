using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Backups.Modules;

public sealed class BlobInventoryServices : IServiceModule
{
    // TODO: an empty in-memory inventory stands in for the real ones. One registration here picks the folder, Postgres
    // or S3 inventory by BlobStoreChoice, as BlobStoreServices picks the store.
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IBlobInventory>(new InMemoryBlobInventory(new InMemoryBlobStore()));
}
