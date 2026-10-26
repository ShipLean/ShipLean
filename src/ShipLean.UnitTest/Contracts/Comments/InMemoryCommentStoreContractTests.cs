using ShipLean.Application.Fakes.Comments;
using ShipLean.Application.Markdown;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.Comments;
using ShipLean.TestSupport.Contracts.Mcp;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.UnitTest.Contracts.Comments;

[TestFixture]
public class InMemoryCommentStoreContractTests : CommentStoreContract
{
    private readonly SettableCurrentUser _user = new();
    private readonly SettableCurrentAgent _agent = new();
    private InMemoryCommentStore _store = null!;

    [SetUp]
    public void CreateWorld()
    {
        _store = new InMemoryCommentStore(_user, new MarkdownRenderer(), TimeProvider.System, _agent);
        _store.AddUser(new UserRef(SeedIds.Users.Joel, "Joel Dickson", "JD", "#5b6b8c", null));
        _store.AddUser(new UserRef(SeedIds.Users.Priya, "Priya Shah", "PS", "#2f7fd8", null));
    }

    protected override ICommentStore CreateStore(Guid actorId)
    {
        _user.Id = actorId;
        _agent.Clear();
        return _store;
    }

    protected override ICommentStore CreateStore(Guid actorId, string agentName, string? agentHost, bool agentVerified)
    {
        _user.Id = actorId;
        _agent.ActAs(agentName, agentHost, agentVerified);
        return _store;
    }

    protected override Task<Guid> ArrangeWorkItemAsync()
    {
        var id = Guid.NewGuid();
        _store.AddWorkItem(id);
        return Task.FromResult(id);
    }
}
