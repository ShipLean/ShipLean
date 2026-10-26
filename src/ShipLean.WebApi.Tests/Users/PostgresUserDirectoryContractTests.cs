using ShipLean.Application.Features.Users;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.WebApi.Tests.Users;

public sealed class PostgresUserDirectoryContractTests : UserDirectoryContract
{
    private PostgresUsersStore _store = null!;
    private UserDirectory _directory = null!;

    [SetUp]
    public async Task CloneDatabase()
    {
        _store = await PostgresUsersStore.CreateAsync();
        _directory = new UserDirectory(_store.DataSource, TimeProvider.System);
    }

    [TearDown]
    public async Task DropDatabase() => await _store.DisposeAsync();

    protected override IUserDirectory Directory => _directory;

    protected override Task<ArrangedUser> ArrangeUserAsync(UserSpec spec) => _store.ArrangeUserAsync(spec);

    protected override Task<int> CountUsersWithEmailAsync(string email) => _store.CountUsersWithEmailAsync(email);

    protected override Task<Guid> ArrangeProjectAsync() => _store.ArrangeProjectAsync();

    protected override Task ArchiveMembershipAsync(Guid projectId, Guid userId) => _store.ArchiveMembershipAsync(projectId, userId);
}
