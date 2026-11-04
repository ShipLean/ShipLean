using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// IAttachmentStore.StageAsync and CommitStagedAsync on Postgres, with the item total pinned at 100 bytes.
[TestFixture]
public class AttachmentCommitTests
{
    private const int Limit = 100;

    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private NpgsqlDataSource _singleConnection = null!;
    private CountingProjectEvents _events = null!;
    private BusyCountingBlobStore _blobs = null!;
    private AttachmentStore _store = null!;
    private Guid _workItem;

    [OneTimeSetUp]
    public async Task CreateDatabase()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        var single = new NpgsqlConnectionStringBuilder(DatabaseServices.PinUtc(_database.ConnectionString)) { MaxPoolSize = 1, Timeout = 1 };
        _singleConnection = NpgsqlDataSource.Create(single.ConnectionString);
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _singleConnection.DisposeAsync();
        await _database.DisposeAsync();
    }

    [SetUp]
    public async Task ArrangeStore()
    {
        _events = new CountingProjectEvents();
        _blobs = new BusyCountingBlobStore(new InMemoryBlobStore(), _singleConnection);
        _store = new AttachmentStore(_dataSource, new FixedCurrentUser(SeedIds.Users.Joel), _blobs, TimeProvider.System, _events, new AttachmentLimits(Limit, Limit));
        await using var connection = await _database.OpenAsync();
        _workItem = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
    }

    [Test]
    public async Task Commit_WhenReplacingEveryFile_ShouldCountOnlyTheNewOnes()
    {
        await _store.AddAsync(_workItem, "a.bin", "application/octet-stream", Zeros(60));
        await _store.AddAsync(_workItem, "b.bin", "application/octet-stream", Zeros(30));
        var newB = await StageAsync("b.bin", 30);
        var newA = await StageAsync("a.bin", 60);

        var committed = await _store.CommitStagedAsync(_workItem, [newB, newA]);

        committed.Select(a => a.Id).ShouldBe([newB.Id, newA.Id]);
        (await LiveIdsAsync()).ShouldBe([newB.Id, newA.Id], ignoreOrder: true);
    }

    [Test]
    public async Task Commit_WhenOtherFilesOnTheItem_ShouldCountThem()
    {
        var other = await _store.AddAsync(_workItem, "other.bin", "application/octet-stream", Zeros(50));
        var first = await StageAsync("a.bin", 40);
        var second = await StageAsync("b.bin", 20);

        var error = await Should.ThrowAsync<ApiException>(() => _store.CommitStagedAsync(_workItem, [first, second]));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await LiveIdsAsync()).ShouldBe([other.Id]);
    }

    [Test]
    public async Task Commit_WhenOverTheItemTotal_ShouldBeValidationAndChangeNothing()
    {
        var old = await _store.AddAsync(_workItem, "a.bin", "application/octet-stream", Zeros(10));
        var first = await StageAsync("a.bin", 60);
        var second = await StageAsync("b.bin", 60);
        var published = _events.Published.Count;

        var error = await Should.ThrowAsync<ApiException>(() => _store.CommitStagedAsync(_workItem, [first, second]));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Message.ShouldBe(new AttachmentLimits(Limit, Limit).ItemFullMessage);
        (await LiveIdsAsync()).ShouldBe([old.Id]);
        _events.Published.Count.ShouldBe(published);
    }

    [Test]
    public async Task Commit_WhenHookWritesInItsTransaction_ShouldRollBackWithTheRows()
    {
        var old = await _store.AddAsync(_workItem, "a.bin", "application/octet-stream", Zeros(10));
        var staged = await StageAsync("a.bin", 10);

        await Should.ThrowAsync<InvalidOperationException>(() => _store.CommitStagedAsync(_workItem, [staged], async (transaction, ct) =>
        {
            await transaction.Connection!.ExecuteAsync(new CommandDefinition(
                "UPDATE work_item SET title = 'changed by the hook' WHERE id = @id", new { id = _workItem }, transaction, cancellationToken: ct));
            throw new InvalidOperationException("hook failed");
        }));

        await using var connection = await _database.OpenAsync();
        (await connection.ExecuteScalarAsync<string>("SELECT title FROM work_item WHERE id = @id", new { id = _workItem })).ShouldNotBe("changed by the hook");
        (await LiveIdsAsync()).ShouldBe([old.Id]);
    }

    [Test]
    public async Task Commit_WhenHookReadsItsTransaction_ShouldSeeTheNewRowsAndNotTheOldOnes()
    {
        await _store.AddAsync(_workItem, "a.bin", "application/octet-stream", Zeros(10));
        var staged = await StageAsync("a.bin", 10);
        Guid[] seen = [];

        await _store.CommitStagedAsync(_workItem, [staged], async (transaction, ct) =>
            seen = [.. await transaction.Connection!.QueryAsync<Guid>(new CommandDefinition(
                "SELECT id FROM attachment WHERE work_item_id = @id AND archived_at IS NULL", new { id = _workItem }, transaction, cancellationToken: ct))]);

        seen.ShouldBe([staged.Id]);
    }

    [Test]
    public async Task Commit_ShouldPublishOnceAfterTheCommit()
    {
        var first = await StageAsync("a.bin", 10);
        var second = await StageAsync("b.bin", 10);
        var duringHook = -1;

        await _store.CommitStagedAsync(_workItem, [first, second], (_, _) =>
        {
            duringHook = _events.Published.Count;
            return Task.CompletedTask;
        });

        duringHook.ShouldBe(0);
        _events.Published.OfType<AttachmentsChanged>().Count(e => e.WorkItemId == _workItem).ShouldBe(1);
    }

    [Test]
    public async Task Stage_WhenMaxBytesGiven_ShouldUseItInsteadOfTheFileLimit()
    {
        var staged = await _store.StageAsync(_workItem, "big.bin", "application/octet-stream", Zeros(Limit + 50), maxBytes: Limit + 100);

        staged.SizeBytes.ShouldBe(Limit + 50);
        var error = await Should.ThrowAsync<ApiException>(() => _store.StageAsync(_workItem, "small.bin", "application/octet-stream", Zeros(50), maxBytes: 10));
        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Stage_WhenNoMaxBytes_ShouldUseTheFileLimit()
    {
        var error = await Should.ThrowAsync<ApiException>(() => _store.StageAsync(_workItem, "big.bin", "application/octet-stream", Zeros(Limit + 1)));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await _store.StageAsync(_workItem, "edge.bin", "application/octet-stream", Zeros(Limit))).SizeBytes.ShouldBe(Limit);
    }

    [Test]
    public async Task Stage_ShouldHoldNoConnectionDuringTheBlobWrite()
    {
        var store = new AttachmentStore(_singleConnection, new FixedCurrentUser(SeedIds.Users.Joel), _blobs, TimeProvider.System, _events, new AttachmentLimits(Limit, Limit));

        await store.StageAsync(_workItem, "a.bin", "application/octet-stream", Zeros(10));

        _blobs.ConnectionFreeDuringPut.ShouldBe(true);
    }

    [Test]
    public async Task Commit_WhenConcurrentWithAddOfTheSameName_ShouldLeaveOneLiveRow()
    {
        await _store.AddAsync(_workItem, "race.bin", "application/octet-stream", Zeros(10));
        var staged = await StageAsync("race.bin", 10);
        var inHook = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var commit = _store.CommitStagedAsync(_workItem, [staged], async (_, _) =>
        {
            inHook.SetResult();
            await release.Task;
        });
        if (await Task.WhenAny(inHook.Task, commit) == commit)
        {
            await commit;
        }

        var add = _store.AddAsync(_workItem, "race.bin", "application/octet-stream", Zeros(10));
        release.SetResult();
        await Task.WhenAll(commit, add);

        await using var connection = await _database.OpenAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM attachment WHERE work_item_id = @id AND name = 'race.bin' AND archived_at IS NULL", new { id = _workItem })).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM attachment WHERE work_item_id = @id AND name = 'race.bin'", new { id = _workItem })).ShouldBe(3);
    }

    private Task<StagedAttachment> StageAsync(string name, int size)
        => _store.StageAsync(_workItem, name, "application/octet-stream", Zeros(size));

    private async Task<Guid[]> LiveIdsAsync()
    {
        await using var connection = await _database.OpenAsync();
        return [.. await connection.QueryAsync<Guid>("SELECT id FROM attachment WHERE work_item_id = @id AND archived_at IS NULL", new { id = _workItem })];
    }

    private static MemoryStream Zeros(int count) => new(new byte[count]);
}
