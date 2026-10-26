using Dapper;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// One scenario on one seeded database clone per class; every test arranges a project of its own.
public abstract class PostgresWorkItemStoreTestBase
{
    protected static Guid Actor => SeedIds.Users.Joel;

    protected PostgresWorkItemStoreArrangement Arrangement { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task CreateDatabase() => Arrangement = await PostgresWorkItemStoreArrangement.CreateAsync();

    [OneTimeTearDown]
    public async Task DropDatabase() => await Arrangement.DisposeAsync();

    protected WorkItemStore PassThroughStore() => Arrangement.CreateStore(new PassThroughWorkItemRules(), Actor);

    protected static async Task<WriteResult> CreateAsync(IWorkItemStore store, ArrangedProject project, WorkItemTypeId type, string title, Guid? parentId = null, WorkItemChanges? changes = null)
        => WorkItemContractSupport.Written(await store.CreateAsync(new WorkItemCreate(project.ProjectId, type, title, parentId, null, changes), WriteOptions.User));

    protected static async Task<WriteResult> PatchAsync(IWorkItemStore store, Guid id, int expectedRevision, WorkItemChanges changes)
        => WorkItemContractSupport.Written(await store.PatchAsync(id, new WorkItemPatch(expectedRevision, changes), WriteOptions.User));

    protected async Task<T> QueryAsync<T>(string sql, object parameters)
    {
        await using var connection = await Arrangement.Database.OpenAsync();
        return await connection.QuerySingleAsync<T>(sql, parameters);
    }

    protected async Task ExecuteAsync(string sql, object parameters)
    {
        await using var connection = await Arrangement.Database.OpenAsync();
        await connection.ExecuteAsync(sql, parameters);
    }
}
