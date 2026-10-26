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

    private static ImportedAttachment Imported(string id, string name, DateTimeOffset createdAt, Guid? uploader = null)
        => new("jira", id, name, "image/png", 0, uploader, createdAt);

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));
}
