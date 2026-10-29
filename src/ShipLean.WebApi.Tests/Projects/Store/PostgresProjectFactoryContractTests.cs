using ShipLean.Application.Fakes.Holidays;
using ShipLean.Application.Features.Sprints.Metrics;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Fakes.Criteria;
using ShipLean.Application.Features.Boards.Settings;
using ShipLean.Application.Features.Projects;
using ShipLean.Application.Features.States;
using ShipLean.Application.Features.Sprints;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Projects;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.TestSupport.Database;
using ShipLean.WebApi.Tests.Boards.Settings;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Projects.Store;

// The project factory contract on Postgres: one empty-schema clone for the class (every test uses its own random key and users),
// the real board settings and sprint stores behind the factory.
public sealed class PostgresProjectFactoryContractTests : ProjectFactoryContract
{
    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase()
    {
        DapperTypeHandlers.Register();
        _database = await ShipLeanDatabase.CloneEmptyAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override IProjectFactory CreateFactory(Guid actorId)
    {
        var user = new SettableCurrentUser { Id = actorId };
        var boards = new BoardSettingsStore(_dataSource, TimeProvider.System, new InMemoryCriteriaEvaluator(user), new DiscardingProjectEvents());
        var sprints = new SprintStore(_dataSource, TimeProvider.System, new DiscardingProjectEvents(), new ThrowingWorkItemStore(), new InMemoryPublicHolidays(), new SprintMetrics(_dataSource, TimeProvider.System), new SprintCapacityRecompute(_dataSource, TimeProvider.System, new InMemoryPublicHolidays()));
        return new ProjectFactory(_dataSource, TimeProvider.System, user, boards, sprints, new StateCatalog(_dataSource));
    }

    [Test]
    public async Task Create_WhenCopyFromUnknownProject_ShouldBeNotFound()
    {
        var creator = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");
        var key = new string([.. Enumerable.Range(0, 4).Select(_ => (char)('A' + Random.Shared.Next(26)))]);

        var thrown = await Should.ThrowAsync<ApiException>(() => CreateFactory(creator).CreateAsync(new ProjectCreate("Copy", key, Guid.NewGuid(), [], [1, 2, 3], null)));

        thrown.Code.ShouldBe(ApiErrorCodes.NotFound);
        await using var connection = await _database.OpenAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM project WHERE key = @key", new { key })).ShouldBe(0);
    }

    protected override async Task<Guid> ArrangeUserAsync(string email)
    {
        await using var connection = await _database.OpenAsync();
        return await new AppUserBuilder().WithEmail(email).InsertAsync(connection);
    }

    protected override async Task<Guid> ArrangeProjectAsync(string key)
    {
        var id = Guid.NewGuid();
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO project (id, key, name) VALUES (@id, @key, @name)", new { id, key, name = "Arranged " + key });
        return id;
    }

    protected override async Task<IReadOnlyList<(Guid UserId, ProjectRole Role)>> ReadMembersAsync(Guid projectId)
    {
        await using var connection = await _database.OpenAsync();
        var rows = await connection.QueryAsync<(Guid UserId, string Role)>(
            "SELECT user_id, role FROM project_member WHERE project_id = @projectId AND archived_at IS NULL", new { projectId });
        return [.. rows.Select(r => (r.UserId, Enum.Parse<ProjectRole>(r.Role, true)))];
    }

    protected override async Task<int> CountSprintsAsync(Guid projectId)
    {
        await using var connection = await _database.OpenAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM sprint WHERE project_id = @projectId", new { projectId });
    }

    // Made by the factory itself (so it has boards), then given the extra state and one item.
    protected override async Task<Guid> ArrangeSourceProjectAsync(string key, string stateName)
    {
        var owner = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");
        var project = await CreateFactory(owner).CreateAsync(new ProjectCreate($"Source {key}", key, null, [], [1, 2, 3, 4, 5], null));
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO state (id, project_id, type, name, category, sort_order)
            VALUES (@id, @projectId, 'story', @stateName, 'inProgress', 10)
            """,
            new { id = Guid.NewGuid(), projectId = project.Id, stateName });
        await new WorkItemBuilder(project.Id).InsertAsync(connection);
        return project.Id;
    }

    protected override async Task<IReadOnlyList<(WorkItemTypeId Type, string Name)>> ReadStatesAsync(Guid projectId)
    {
        await using var connection = await _database.OpenAsync();
        var rows = await connection.QueryAsync<(string Type, string Name)>(
            "SELECT type, name FROM state WHERE project_id = @projectId AND archived_at IS NULL", new { projectId });
        return [.. rows.Select(r => (Enum.Parse<WorkItemTypeId>(r.Type, true), r.Name))];
    }

    protected override async Task<int> CountItemsAsync(Guid projectId)
    {
        await using var connection = await _database.OpenAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @projectId", new { projectId });
    }
}
