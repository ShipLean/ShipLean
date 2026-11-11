using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport.Backups;

namespace ShipLean.WebApi.Tests.Backups.Inventory;

[TestFixture]
public class InMemoryBlobInventoryContractTests : BlobInventoryContract
{
    protected override (IBlobStore Store, IBlobInventory Inventory) Create()
    {
        var store = new InMemoryBlobStore();
        return (store, new InMemoryBlobInventory(store));
    }
}
