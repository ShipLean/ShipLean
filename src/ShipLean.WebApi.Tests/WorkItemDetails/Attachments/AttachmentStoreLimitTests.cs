using System.Text;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;
using ShipLean.WebApi.Tests.Boards.Settings;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// The size limit on the store itself: an upload or an import over it is refused while streaming and nothing is kept.
[TestFixture]
public class AttachmentStoreLimitTests
{
    private const int Limit = 1024;

    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private InMemoryBlobStore _blobs = null!;
    private AttachmentStore _store = null!;
    private Guid _workItem;

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

    [SetUp]
    public async Task ArrangeStore()
    {
        _blobs = new InMemoryBlobStore();
        _store = new AttachmentStore(_dataSource, new FixedCurrentUser(SeedIds.Users.Joel), _blobs, TimeProvider.System, new DiscardingProjectEvents(), new AttachmentLimits(Limit));
        await using var connection = await _database.OpenAsync();
        _workItem = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
    }

    [Test]
    public async Task Add_WhenOverTheLimit_ShouldBeValidationAndKeepNothing()
    {
        var error = await Should.ThrowAsync<ApiException>(() => _store.AddAsync(_workItem, "big.bin", "application/octet-stream", Zeros(Limit + 1)));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await RowCountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Add_WhenExactlyTheLimit_ShouldStoreIt()
    {
        var added = await _store.AddAsync(_workItem, "edge.bin", "application/octet-stream", Zeros(Limit));

        added.SizeBytes.ShouldBe(Limit);
    }

    [Test]
    public async Task Import_WhenStreamIsLongerThanTheLimit_ShouldStopReadingAndKeepNothing()
    {
        var endless = new EndlessStream();
        var imported = new ImportedAttachment("jira", "30001", "huge.bin", "application/octet-stream", 0, null, DateTimeOffset.UnixEpoch);

        var error = await Should.ThrowAsync<ApiException>(() => _store.ImportAsync(_workItem, imported, endless));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        endless.BytesRead.ShouldBeLessThan(Limit * 100L);
        (await RowCountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Import_WhenRerunIsOverTheLimit_ShouldKeepTheOldBytes()
    {
        var imported = new ImportedAttachment("jira", "30002", "ok.bin", "text/plain", 0, null, DateTimeOffset.UnixEpoch);
        var id = await _store.ImportAsync(_workItem, imported, new MemoryStream(Encoding.UTF8.GetBytes("old")));

        await Should.ThrowAsync<ApiException>(() => _store.ImportAsync(_workItem, imported, Zeros(Limit + 1)));

        var (_, content) = await _store.OpenAsync(id);
        await using var stream = content.Content;
        (await new StreamReader(stream).ReadToEndAsync()).ShouldBe("old");
    }

    [TestCase("a\0b.txt")]
    [TestCase("a\r\nb.txt")]
    public async Task Import_WhenNameHasControlCharacters_ShouldBeValidationAndKeepNothing(string name)
    {
        var imported = new ImportedAttachment("jira", "30003", name, "text/plain", 0, null, DateTimeOffset.UnixEpoch);

        var error = await Should.ThrowAsync<ApiException>(() => _store.ImportAsync(_workItem, imported, Zeros(1)));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await RowCountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Import_WhenContentTypeIsHuge_ShouldStoreOctetStream()
    {
        var imported = new ImportedAttachment("jira", "30004", "huge.txt", "text/" + new string('a', 3000), 0, null, DateTimeOffset.UnixEpoch);

        var id = await _store.ImportAsync(_workItem, imported, Zeros(1));

        var (attachment, content) = await _store.OpenAsync(id);
        await content.Content.DisposeAsync();
        attachment.ContentType.ShouldBe("application/octet-stream");
    }

    private async Task<int> RowCountAsync()
    {
        await using var connection = await _database.OpenAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM attachment WHERE work_item_id = @id", new { id = _workItem });
    }

    private static MemoryStream Zeros(int count) => new(new byte[count]);

    private sealed class EndlessStream : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = Math.Min(buffer.Length, 256);
            buffer.Span[..count].Clear();
            BytesRead += count;
            return ValueTask.FromResult(count);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
