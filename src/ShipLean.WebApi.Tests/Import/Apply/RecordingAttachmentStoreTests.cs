using ShipLean.Application.Fakes.Attachments;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.WebApi.Tests.Import.Apply;

[TestFixture]
public class RecordingAttachmentStoreTests
{
    [Test]
    public async Task StageAndCommit_WhenCalled_ShouldGoToTheInnerStore()
    {
        var user = new SettableCurrentUser { Id = SeedIds.Users.Joel };
        var inner = new InMemoryAttachmentStore(user, new InMemoryBlobStore(), TimeProvider.System);
        var workItem = Guid.NewGuid();
        inner.AddWorkItem(workItem);
        inner.AddUser(new UserRef(SeedIds.Users.Joel, "Joel Dickson", "JD", "#5b6b8c", null));
        var store = new RecordingAttachmentStore(inner);

        var staged = await store.StageAsync(workItem, "a.txt", "text/plain", new MemoryStream("abc"u8.ToArray()));
        var committed = await store.CommitStagedAsync(workItem, [staged]);

        committed.Select(a => a.Name).ShouldBe(["a.txt"]);
        (await inner.ListAsync(workItem)).Select(a => a.Id).ShouldBe(committed.Select(a => a.Id));
    }
}
