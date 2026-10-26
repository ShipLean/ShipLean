using ShipLean.Application.Fakes.Users;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.UnitTest.Contracts.Users;

public sealed class InMemoryUserDirectoryContractTests : UserDirectoryContract
{
    private InMemoryUsers _users = null!;
    private InMemoryUserDirectory _directory = null!;

    [SetUp]
    public void CreateStore()
    {
        _users = new InMemoryUsers();
        _directory = new InMemoryUserDirectory(_users);
    }

    protected override IUserDirectory Directory => _directory;

    protected override Task<ArrangedUser> ArrangeUserAsync(UserSpec spec) => Task.FromResult(InMemoryUserArranger.Arrange(_users, spec));

    protected override Task<int> CountUsersWithEmailAsync(string email) => Task.FromResult(_users.CountWithEmail(email));

    protected override Task<Guid> ArrangeProjectAsync() => Task.FromResult(Guid.NewGuid());

    protected override Task ArchiveMembershipAsync(Guid projectId, Guid userId)
    {
        _users.ArchiveMembership(projectId, userId);
        return Task.CompletedTask;
    }
}
