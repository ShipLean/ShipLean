using System.Text.Json;
using Dapper;
using ShipLean.Application.Criteria;
using ShipLean.Application.Features.WorkItems.Reads.Tree;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.TestSupport.Contracts.WorkItems;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.WorkItems.Reads.Tree;

// One seeded clone per class; every test arranges a project of its own and builds items with WorkItemBuilder.
public abstract class WorkTreeReadsTestBase
{
    private PostgresWorkItemStoreArrangement _arrangement = null!;
    private Npgsql.NpgsqlDataSource _dataSource = null!;

    protected CriteriaEvaluator Evaluator { get; } = new(new SettableCurrentUser());

    protected WorkTreeReads Reads { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task CreateDatabase()
    {
        _arrangement = await PostgresWorkItemStoreArrangement.CreateAsync();
        _dataSource = Npgsql.NpgsqlDataSource.Create(Application.Database.DatabaseServices.PinUtc(_arrangement.Database.ConnectionString));
        Reads = new WorkTreeReads(_dataSource, Evaluator, TimeProvider.System);
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _arrangement.DisposeAsync();
    }

    protected Task<ArrangedProject> ArrangeProjectAsync() => _arrangement.ArrangeProjectAsync();

    // Inserts the item described by `configure` into the project and returns its id.
    protected async Task<Guid> AddAsync(ArrangedProject project, string type, Action<WorkItemBuilder>? configure = null)
    {
        var builder = new WorkItemBuilder(project.ProjectId).WithType(type);
        configure?.Invoke(builder);
        await using var connection = await _arrangement.Database.OpenAsync();
        return await builder.InsertAsync(connection);
    }

    protected async Task<Guid> AddSprintAsync(ArrangedProject project, string name)
    {
        var id = Guid.NewGuid();
        await using var connection = await _arrangement.Database.OpenAsync();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @projectId, @name, current_date - 3, current_date + 3)",
            new { id, projectId = project.ProjectId, name });
        return id;
    }

    protected async Task SetRevisionAsync(Guid itemId, int revision)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await connection.ExecuteAsync("UPDATE work_item SET revision = @revision WHERE id = @itemId", new { itemId, revision });
    }

    protected async Task SetParentAsync(Guid itemId, Guid parentId)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await connection.ExecuteAsync("UPDATE work_item SET parent_id = @parentId, parent_type = (SELECT p.type FROM work_item p WHERE p.id = @parentId) WHERE id = @itemId", new { itemId, parentId });
    }

    protected async Task SetCriteriaAsync(Guid itemId)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await connection.ExecuteAsync("UPDATE work_item SET acceptance_criteria = '[{\"text\":\"It works\",\"done\":false}]'::jsonb WHERE id = @itemId", new { itemId });
    }

    protected static CriteriaJson Where(string field, CriteriaOp op, string value)
        => new(CriteriaMatch.All, [new CriteriaClause(field, op, JsonSerializer.SerializeToElement(value))]);

    protected static Guid Closed(ArrangedProject project, string type) => project.StateId(Enum.Parse<WorkItemTypeId>(type, ignoreCase: true), StateCategory.Completed);

    protected static Guid Removed(ArrangedProject project, string type) => project.StateId(Enum.Parse<WorkItemTypeId>(type, ignoreCase: true), StateCategory.Removed);

    protected static Guid Active(ArrangedProject project, string type) => project.StateId(Enum.Parse<WorkItemTypeId>(type, ignoreCase: true), StateCategory.InProgress);
}
