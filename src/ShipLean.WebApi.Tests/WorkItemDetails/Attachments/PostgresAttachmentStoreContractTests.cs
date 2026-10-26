using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Attachments;
using ShipLean.TestSupport.Database;
using ShipLean.WebApi.Tests.Boards.Settings;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// AttachmentStoreContract on Postgres rows with the real folder blob store: one seeded clone and one blob folder for the class, a new Ship story for every test.
[TestFixture]
public class PostgresAttachmentStoreContractTests : AttachmentStoreContract
{
    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private string _blobRoot = null!;

    [OneTimeSetUp]
    public async Task CreateDatabase()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        _blobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-blobs-{Guid.NewGuid():N}");
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
        Directory.Delete(_blobRoot, recursive: true);
    }

    protected override IAttachmentStore CreateStore(Guid actorId)
        => new AttachmentStore(
            _dataSource, new FixedCurrentUser(actorId), new LocalDiskBlobStore(_blobRoot), TimeProvider.System,
            new DiscardingProjectEvents(), new AttachmentLimits(AttachmentLimits.DefaultMaxBytes));

    protected override async Task<Guid> ArrangeWorkItemAsync()
    {
        await using var connection = await _database.OpenAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
    }
}
