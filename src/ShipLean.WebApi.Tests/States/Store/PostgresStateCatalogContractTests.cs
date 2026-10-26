using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.States;
using ShipLean.Contracts.Projects;
using ShipLean.TestSupport.Contracts.States;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.States.Store;

// The state catalog contract on Postgres: one seeded clone for the class and a new project for every arranged project.
// The catalog joins the caller's transaction and the contract never commits it, so the catalog under test is wrapped to
// commit (or roll back) the transaction it was handed, as the caller would.
public sealed class PostgresStateCatalogContractTests : StateCatalogContract
{
    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase()
    {
        DapperTypeHandlers.Register();
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override IStateCatalog CreateCatalog() => new CommittingStateCatalog(new StateCatalog(_dataSource));

    protected override async Task<Guid> ArrangeProjectAsync(bool withDefaultStates)
    {
        var id = Guid.NewGuid();
        var key = new string([.. Enumerable.Range(0, 4).Select(_ => (char)('A' + Random.Shared.Next(26)))]);
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO project (id, key, name) VALUES (@id, @key, 'Contract project')", new { id, key });
        if (withDefaultStates)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await new StateCatalog(_dataSource).CreateDefaultsAsync(id, transaction);
            await transaction.CommitAsync();
        }

        return id;
    }

    protected override async Task<DbTransaction> BeginTransactionAsync() => await (await _dataSource.OpenConnectionAsync()).BeginTransactionAsync();

    protected override async Task<Guid> ArrangeItemAsync(Guid projectId, Guid stateId)
    {
        await using var connection = await _database.OpenAsync();
        var type = await connection.ExecuteScalarAsync<string>("SELECT type FROM state WHERE id = @stateId", new { stateId });
        return await new ShipLean.TestSupport.Builders.WorkItemBuilder(projectId).WithType(type!).WithState(stateId).InsertAsync(connection);
    }

    protected override async Task<Guid> ReadItemStateAsync(Guid itemId)
    {
        await using var connection = await _database.OpenAsync();
        return await connection.ExecuteScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @itemId", new { itemId });
    }
}
