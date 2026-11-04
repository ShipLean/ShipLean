using System.Data.Common;
using System.Text;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Attachments;

// Behaviour every IAttachmentStore must have, whatever it stores data in. Users are the seeded Joel and Priya.
[TestFixture]
public abstract class AttachmentStoreContract
{
    private static readonly Guid Joel = SeedIds.Users.Joel;
    private static readonly Guid Priya = SeedIds.Users.Priya;

    protected abstract IAttachmentStore CreateStore(Guid actorId);

    protected abstract Task<Guid> ArrangeWorkItemAsync();

    [Test]
    public async Task Open_WhenAdded_ShouldReturnTheSameBytes()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);

        var added = await store.AddAsync(workItem, "notes.txt", "text/plain", Bytes("hello bytes"));
        var (attachment, content) = await store.OpenAsync(added.Id);

        await using var stream = content.Content;
        (await new StreamReader(stream).ReadToEndAsync()).ShouldBe("hello bytes");
        content.ContentType.ShouldBe("text/plain");
        attachment.Id.ShouldBe(added.Id);
        added.Name.ShouldBe("notes.txt");
        added.ContentType.ShouldBe("text/plain");
        added.SizeBytes.ShouldBe(11);
        added.UploadedBy.ShouldNotBeNull().Id.ShouldBe(Joel);
        added.DownloadUrl.ShouldBe($"/api/attachments/{added.Id}/content");
        added.CreatedLabel.ShouldNotBeNullOrWhiteSpace();
        (await store.ListAsync(workItem)).Select(a => a.Id).ShouldBe([added.Id]);
    }

    [Test]
    public async Task Add_WhenWorkItemIsUnknown_ShouldBeNotFound()
    {
        var store = CreateStore(Joel);

        var error = await Should.ThrowAsync<ApiException>(() => store.AddAsync(Guid.NewGuid(), "a.txt", "text/plain", Bytes("x")));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Open_WhenUnknown_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => CreateStore(Joel).OpenAsync(Guid.NewGuid()));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Open_WhenArchived_ShouldBeNotFound()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var kept = await store.AddAsync(workItem, "kept.txt", "text/plain", Bytes("kept"));
        var gone = await store.AddAsync(workItem, "gone.txt", "text/plain", Bytes("gone"));

        await store.ArchiveAsync(gone.Id);

        var error = await Should.ThrowAsync<ApiException>(() => store.OpenAsync(gone.Id));
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        (await store.ListAsync(workItem)).Select(a => a.Id).ShouldBe([kept.Id]);
    }

    [Test]
    public async Task Import_WhenRerun_ShouldUpdateNotDuplicate()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var created = new DateTimeOffset(2025, 3, 2, 9, 30, 0, TimeSpan.Zero);
        var first = await store.ImportAsync(workItem, Imported("20001", "shot.png", created), Bytes("old"));

        var second = await store.ImportAsync(workItem, Imported("20001", "shot-v2.png", created.AddDays(2)), Bytes("newer bytes"));

        second.ShouldBe(first);
        var listed = await store.ListAsync(workItem);
        listed.Count.ShouldBe(1);
        listed[0].Name.ShouldBe("shot-v2.png");
        listed[0].SizeBytes.ShouldBe(11);
        listed[0].CreatedAt.ShouldBe(created);
        var (_, content) = await store.OpenAsync(first);
        await using var stream = content.Content;
        (await new StreamReader(stream).ReadToEndAsync()).ShouldBe("newer bytes");
    }

    [Test]
    public async Task Import_WhenRerunStreamFails_ShouldKeepTheOldBytes()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var created = new DateTimeOffset(2025, 3, 2, 9, 30, 0, TimeSpan.Zero);
        var id = await store.ImportAsync(workItem, Imported("20002", "shot.png", created), Bytes("old"));

        await Should.ThrowAsync<IOException>(() => store.ImportAsync(workItem, Imported("20002", "shot-v2.png", created), new FailingStream("partial")));

        var listed = (await store.ListAsync(workItem)).ShouldHaveSingleItem();
        listed.Name.ShouldBe("shot.png");
        listed.SizeBytes.ShouldBe(3);
        var (_, content) = await store.OpenAsync(id);
        await using var stream = content.Content;
        (await new StreamReader(stream).ReadToEndAsync()).ShouldBe("old");
    }

    [Test]
    public async Task Import_WhenArchivedThenRerun_ShouldStayHidden()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var created = new DateTimeOffset(2025, 3, 2, 9, 30, 0, TimeSpan.Zero);
        var id = await store.ImportAsync(workItem, Imported("20003", "shot.png", created), Bytes("old"));
        await store.ArchiveAsync(id);

        var again = await store.ImportAsync(workItem, Imported("20003", "shot.png", created), Bytes("old"));

        again.ShouldBe(id);
        (await store.ListAsync(workItem)).ShouldBeEmpty();
        var error = await Should.ThrowAsync<ApiException>(() => store.OpenAsync(id));
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Import_WhenImported_ShouldKeepTheUploaderAndCreatedTime()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var created = new DateTimeOffset(2025, 3, 2, 9, 30, 0, TimeSpan.Zero);

        var byUser = await store.ImportAsync(workItem, Imported("1", "a.png", created, Priya), Bytes("a"));
        var byNobody = await store.ImportAsync(workItem, Imported("2", "b.png", created.AddHours(1)), Bytes("b"));

        var listed = await store.ListAsync(workItem);
        listed.Select(a => a.Id).ShouldBe([byUser, byNobody]);
        listed[0].UploadedBy.ShouldNotBeNull().Id.ShouldBe(Priya);
        listed[0].CreatedAt.ShouldBe(created);
        listed[1].UploadedBy.ShouldBeNull();
    }

    [Test]
    public async Task Stage_ShouldChangeNoRow()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var old = await store.AddAsync(workItem, "a.txt", "text/plain", Bytes("old"));

        var staged = await store.StageAsync(workItem, "a.txt", "text/plain", Bytes("new"));

        staged.Name.ShouldBe("a.txt");
        staged.SizeBytes.ShouldBe(3);
        staged.WorkItemId.ShouldBe(workItem);
        (await store.ListAsync(workItem)).Select(a => a.Id).ShouldBe([old.Id]);
        await using var stream = (await store.OpenAsync(old.Id)).Content.Content;
        (await new StreamReader(stream).ReadToEndAsync()).ShouldBe("old");
    }

    [Test]
    public async Task Commit_ShouldReplaceEverySameNameRowTogether()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var oldA = await store.AddAsync(workItem, "a.txt", "text/plain", Bytes("old a"));
        var oldB = await store.AddAsync(workItem, "b.txt", "text/plain", Bytes("old b"));
        var kept = await store.AddAsync(workItem, "keep.txt", "text/plain", Bytes("kept"));
        var newA = await store.StageAsync(workItem, "a.txt", "text/plain", Bytes("new a"));
        var newB = await store.StageAsync(workItem, "b.txt", "text/plain", Bytes("new b"));
        var newC = await store.StageAsync(workItem, "c.txt", "text/plain", Bytes("new c"));

        var committed = await store.CommitStagedAsync(workItem, [newA, newB, newC]);

        committed.Select(a => a.Id).ShouldBe([newA.Id, newB.Id, newC.Id]);
        (await store.ListAsync(workItem)).Select(a => a.Id).OrderBy(i => i).ShouldBe(new[] { kept.Id, newA.Id, newB.Id, newC.Id }.OrderBy(i => i));
        foreach (var gone in new[] { oldA.Id, oldB.Id })
        {
            (await Should.ThrowAsync<ApiException>(() => store.OpenAsync(gone))).Code.ShouldBe(ApiErrorCodes.NotFound);
        }

        await using var stream = (await store.OpenAsync(newA.Id)).Content.Content;
        (await new StreamReader(stream).ReadToEndAsync()).ShouldBe("new a");
    }

    [Test]
    public async Task Commit_WhenHookThrows_ShouldLeaveTheOldRowsLiveAndNoNewRow()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var oldA = await store.AddAsync(workItem, "a.txt", "text/plain", Bytes("old a"));
        var newA = await store.StageAsync(workItem, "a.txt", "text/plain", Bytes("new a"));
        var newB = await store.StageAsync(workItem, "b.txt", "text/plain", Bytes("new b"));

        await Should.ThrowAsync<InvalidOperationException>(() => store.CommitStagedAsync(workItem, [newA, newB], (_, _) => throw new InvalidOperationException("hook failed")));

        (await store.ListAsync(workItem)).Select(a => a.Id).ShouldBe([oldA.Id]);
        (await Should.ThrowAsync<ApiException>(() => store.OpenAsync(newA.Id))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Commit_ShouldRunTheHookAfterTheRowsAreWritten()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var staged = await store.StageAsync(workItem, "a.txt", "text/plain", Bytes("a"));
        var calls = new List<DbTransaction?>();

        await store.CommitStagedAsync(workItem, [staged], (transaction, _) =>
        {
            calls.Add(transaction);
            return Task.CompletedTask;
        });

        calls.Count.ShouldBe(1);
        calls[0].ShouldNotBeNull();
        (await store.ListAsync(workItem)).Select(a => a.Id).ShouldBe([staged.Id]);
    }

    [Test]
    public async Task Commit_WhenNameRepeated_ShouldBeValidationAndChangeNothing()
    {
        var workItem = await ArrangeWorkItemAsync();
        var store = CreateStore(Joel);
        var old = await store.AddAsync(workItem, "a.txt", "text/plain", Bytes("old"));
        var first = await store.StageAsync(workItem, "a.txt", "text/plain", Bytes("one"));
        var second = await store.StageAsync(workItem, "a.txt", "text/plain", Bytes("two"));
        var hookRan = false;

        var error = await Should.ThrowAsync<ApiException>(() => store.CommitStagedAsync(workItem, [first, second], (_, _) =>
        {
            hookRan = true;
            return Task.CompletedTask;
        }));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        hookRan.ShouldBeFalse();
        (await store.ListAsync(workItem)).Select(a => a.Id).ShouldBe([old.Id]);
    }

    private static ImportedAttachment Imported(string id, string name, DateTimeOffset createdAt, Guid? uploader = null)
        => new("jira", id, name, "image/png", 0, uploader, createdAt);

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));
}
