using ShipLean.Application.Features.Users;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.WebApi.Tests.Users;

public sealed class PostgresUserAccountsContractTests : UserAccountsContract
{
    private PostgresUsersStore _store = null!;
    private SettableCurrentUser _currentUser = null!;
    private UserAccounts _accounts = null!;
    private UserDirectory _directory = null!;

    [SetUp]
    public async Task CloneDatabase()
    {
        _store = await PostgresUsersStore.CreateAsync();
        _currentUser = new SettableCurrentUser();
        _accounts = new UserAccounts(_store.DataSource, _currentUser, new Passwords(), TimeProvider.System, new UserSessions(_store.DataSource, TimeProvider.System));
        _directory = new UserDirectory(_store.DataSource, TimeProvider.System);
    }

    [TearDown]
    public async Task DropDatabase() => await _store.DisposeAsync();

    protected override IUserAccounts Accounts => _accounts;

    protected override IUserDirectory Directory => _directory;

    protected override Task<ArrangedUser> ArrangeUserAsync(UserSpec spec) => _store.ArrangeUserAsync(spec);

    protected override Task<DateTimeOffset?> LastSignInAtAsync(Guid id) => _store.LastSignInAtAsync(id);

    protected override void ActAs(Guid userId) => _currentUser.Id = userId;
}
