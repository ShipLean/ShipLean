using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;
using ShipLean.WebApi.Tests.Boards.Settings;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// Imported files that share a name on one item: the later one replaces the earlier, like an upload would.
[TestFixture]
public class AttachmentImportNameApiTests
{
    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;

    [OneTimeSetUp]
    public async Task CreateDatabase()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Test]
    public async Task Import_WhenSameSystemSendsTwoFilesWithOneName_ShouldKeepOnlyTheLaterLive()
    {
        await using var connection = await _database.OpenAsync();
        var workItem = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var store = new AttachmentStore(_dataSource, new FixedCurrentUser(SeedIds.Users.Joel), new InMemoryBlobStore(), TimeProvider.System, new DiscardingProjectEvents(), new AttachmentLimits(1024));

        var first = await store.ImportAsync(workItem, new ImportedAttachment("jira", "40001", "same.txt", "text/plain", 0, null, DateTimeOffset.UnixEpoch), new MemoryStream([1]));
        var second = await store.ImportAsync(workItem, new ImportedAttachment("jira", "40002", "same.txt", "text/plain", 0, null, DateTimeOffset.UnixEpoch), new MemoryStream([2]));

        var live = (await connection.QueryAsync<Guid>("SELECT id FROM attachment WHERE work_item_id = @workItem AND archived_at IS NULL", new { workItem })).ToList();
        live.ShouldBe([second]);
        (await connection.ExecuteScalarAsync<bool>("SELECT archived_at IS NOT NULL FROM attachment WHERE id = @first", new { first })).ShouldBeTrue();
    }
}
