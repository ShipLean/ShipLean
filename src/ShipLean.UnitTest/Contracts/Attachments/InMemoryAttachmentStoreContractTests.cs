using ShipLean.Application.Fakes.Attachments;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.Attachments;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.UnitTest.Contracts.Attachments;

[TestFixture]
public class InMemoryAttachmentStoreContractTests : AttachmentStoreContract
{
    private readonly SettableCurrentUser _user = new();
    private InMemoryAttachmentStore _store = null!;

    [SetUp]
    public void CreateWorld()
    {
        _store = new InMemoryAttachmentStore(_user, new InMemoryBlobStore(), TimeProvider.System);
        _store.AddUser(new UserRef(SeedIds.Users.Joel, "Joel Dickson", "JD", "#5b6b8c", null));
        _store.AddUser(new UserRef(SeedIds.Users.Priya, "Priya Shah", "PS", "#2f7fd8", null));
    }

    protected override IAttachmentStore CreateStore(Guid actorId)
    {
        _user.Id = actorId;
        return _store;
    }

    protected override Task<Guid> ArrangeWorkItemAsync()
    {
        var id = Guid.NewGuid();
        _store.AddWorkItem(id);
        return Task.FromResult(id);
    }
}
