using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Boards.Settings;
using ShipLean.Application.Fakes.Criteria;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Boards;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Boards.Settings;

// The board settings contract on Postgres: a fresh empty-schema clone per test, projects and states arranged by SQL.
public sealed class PostgresBoardSettingsStoreContractTests : BoardSettingsStoreContract
{
    private static int _projectCounter;

    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private BoardSettingsStore _store = null!;

    [SetUp]
    public async Task CloneDatabase()
    {
        DapperTypeHandlers.Register();
        _database = await ShipLeanDatabase.CloneEmptyAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        _store = new BoardSettingsStore(_dataSource, TimeProvider.System, new InMemoryCriteriaEvaluator(new SettableCurrentUser()), new DiscardingProjectEvents());
    }

    [TearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override IBoardSettingsStore Store => _store;

    protected override async Task<Guid> ArrangeProjectAsync(IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> statesByType)
    {
        var number = Interlocked.Increment(ref _projectCounter);
        var id = Guid.NewGuid();
        var key = $"{(char)('A' + (number / 26 % 26))}{(char)('A' + (number % 26))}";
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO project (id, key, name) VALUES (@id, @key, @name)", new { id, key, name = "Arranged project " + key });
        foreach (var (type, states) in statesByType)
        {
            foreach (var (state, index) in states.Select((s, i) => (s, i)))
            {
                await InsertStateAsync(connection, id, type, state, index);
            }
        }
        return id;
    }

    protected override async Task ArrangeStateAsync(Guid projectId, WorkItemTypeId type, StateRef state)
    {
        await using var connection = await _database.OpenAsync();
        var order = await connection.ExecuteScalarAsync<int>(
            "SELECT coalesce(max(sort_order), -1) + 1 FROM state WHERE project_id = @projectId AND type = @type", new { projectId, type = type.ToString().ToLowerInvariant() });
        await InsertStateAsync(connection, projectId, type, state, order);
    }

    protected override async Task ArrangeArchivedStateAsync(Guid projectId, WorkItemTypeId type, Guid stateId)
    {
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync("UPDATE state SET archived_at = now() WHERE id = @stateId AND project_id = @projectId", new { stateId, projectId });
    }

    protected override async Task<Guid> ArrangeConnectionAsync(bool archived)
    {
        var id = Guid.NewGuid();
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO agent_connection (id, type, name, receiver_url, signing_secret_protected, signing_secret_last4, callback_token_hash, callback_token_last4, created_at, updated_at, archived_at)
            VALUES (@id, 'linear-compatible', @name, 'https://agent.example/hook', 'protected', 'abcd', @hash, 'wxyz', now(), now(), CASE WHEN @archived THEN now() END)
            """,
            new { id, name = "Connection " + id, archived, hash = id.ToByteArray() });
        return id;
    }

    protected override async Task ArchiveConnectionAsync(Guid connectionId)
    {
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync("UPDATE agent_connection SET archived_at = now() WHERE id = @connectionId", new { connectionId });
    }

    protected override async Task ArrangeCardInColumnAsync(Guid boardId, string columnId)
    {
        await using var connection = await _database.OpenAsync();
        var projectId = await connection.ExecuteScalarAsync<Guid>("SELECT project_id FROM board WHERE id = @boardId", new { boardId });
        await new WorkItemBuilder(projectId).WithBoardPosition(columnId).InsertAsync(connection);
    }

    protected override async Task InTransactionAsync(Func<DbTransaction, Task> action)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await action(transaction);
        await transaction.CommitAsync();
    }

    private static Task InsertStateAsync(NpgsqlConnection connection, Guid projectId, WorkItemTypeId type, StateRef state, int order)
        => connection.ExecuteAsync(
            "INSERT INTO state (id, project_id, type, name, category, sort_order) VALUES (@id, @projectId, @type, @name, @category, @order)",
            new { id = state.Id, projectId, type = type.ToString().ToLowerInvariant(), name = state.Name, category = CategoryText(state.Category), order });

    private static string CategoryText(StateCategory category) => char.ToLowerInvariant(category.ToString()[0]) + category.ToString()[1..];
}
