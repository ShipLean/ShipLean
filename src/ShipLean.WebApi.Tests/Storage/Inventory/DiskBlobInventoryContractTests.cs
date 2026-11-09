using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Storage;
using ShipLean.Application.Storage.Inventory;
using ShipLean.Contracts.Blobs;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Backups;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Storage.Inventory;

// BlobInventoryContract on a folder: one temporary folder per test.
[TestFixture]
public class DiskBlobInventoryContractTests : BlobInventoryContract
{
    private string _root = null!;

    [SetUp]
    public void MakeRoot() => _root = Path.Combine(Path.GetTempPath(), $"shiplean-inventory-{Guid.NewGuid():N}");

    [TearDown]
    public void RemoveRoot()
    {
        // The module test never writes, so its folder may not exist.
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    protected override (IBlobStore Store, IBlobInventory Inventory) Create()
        => (new LocalDiskBlobStore(_root), new LocalDiskBlobInventory(_root));

    [Test]
    public async Task ListKeys_WhenTempFileLeft_ShouldSkipIt()
    {
        var (store, inventory) = Create();
        await store.PutAsync("attachments/kept", new MemoryStream("x"u8.ToArray()), "text/plain", CancellationToken.None);
        await File.WriteAllBytesAsync(Path.Combine(_root, "attachments", $"kept.{Guid.NewGuid():N}{BlobKey.TempSuffix}"), "x"u8.ToArray());

        var listed = await inventory.ListKeysAsync(CancellationToken.None).ToListAsync();

        listed.ShouldBe(["attachments/kept"]);
    }

    [Test]
    public async Task ListKeys_WhenSymlinkUnderRoot_ShouldSkipIt()
    {
        var (store, inventory) = Create();
        await store.PutAsync("attachments/real", new MemoryStream("x"u8.ToArray()), "text/plain", CancellationToken.None);
        await store.PutAsync("attachments/.x", new MemoryStream("x"u8.ToArray()), "text/plain", CancellationToken.None);
        var outside = Path.Combine(Path.GetTempPath(), $"shiplean-outside-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(outside, "x");
        try
        {
            File.CreateSymbolicLink(Path.Combine(_root, "attachments", "link"), outside);
            Directory.CreateSymbolicLink(Path.Combine(_root, "loop"), _root);

            var listed = await inventory.ListKeysAsync(CancellationToken.None).ToListAsync();

            listed.ShouldBe(["attachments/real", "attachments/.x"], ignoreOrder: true);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Test]
    public async Task Module_WhenNoSettingGiven_ShouldRegisterDiskInventory()
    {
        await using var database = await ShipLeanDatabase.CloneSeedAsync();
        await using var host = ShipLeanHost.Start(database.ConnectionString);

        var inventory = host.Services.GetRequiredService<IBlobInventory>();

        inventory.ShouldBeOfType<LocalDiskBlobInventory>();
    }
}
