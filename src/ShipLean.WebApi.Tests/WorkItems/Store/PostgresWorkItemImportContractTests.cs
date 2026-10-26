using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// WorkItemImportContract on Postgres: one seeded clone for the class, a new project for every test.
[TestFixture]
public class PostgresWorkItemImportContractTests : WorkItemImportContract
{
    private PostgresWorkItemStoreArrangement _arrangement = null!;

    [OneTimeSetUp]
    public async Task CreateDatabase() => _arrangement = await PostgresWorkItemStoreArrangement.CreateAsync();

    [OneTimeTearDown]
    public async Task DropDatabase() => await _arrangement.DisposeAsync();

    protected override IWorkItemStore CreateStore(IWorkItemRules rules, Guid actorId) => _arrangement.CreateStore(rules, actorId);

    protected override Task<ArrangedProject> ArrangeProjectAsync() => _arrangement.ArrangeProjectAsync();
}
