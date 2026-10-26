using ShipLean.Application.Fakes.Users;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.UnitTest.Contracts.Users;

public sealed class InMemoryUserAccountsContractTests : UserAccountsContract
{
    private InMemoryUsers _users = null!;
    private SettableCurrentUser _currentUser = null!;
    private InMemoryUserAccounts _accounts = null!;
    private InMemoryUserDirectory _directory = null!;

    [SetUp]
    public void CreateStore()
    {
        _users = new InMemoryUsers();
        _currentUser = new SettableCurrentUser();
        _accounts = new InMemoryUserAccounts(_users, _currentUser);
        _directory = new InMemoryUserDirectory(_users);
    }

    protected override IUserAccounts Accounts => _accounts;

    protected override IUserDirectory Directory => _directory;

    protected override Task<ArrangedUser> ArrangeUserAsync(UserSpec spec) => Task.FromResult(InMemoryUserArranger.Arrange(_users, spec));

    protected override Task<DateTimeOffset?> LastSignInAtAsync(Guid id) => Task.FromResult(_users.LastSignInAt(id));

    protected override void ActAs(Guid userId) => _currentUser.Id = userId;
}
