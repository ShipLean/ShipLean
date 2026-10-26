using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Sprints.Metrics;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Metrics;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.Sprints.Metrics;

// The sprint metrics contract on Postgres: one seeded clone for the class and a new project for every arranged project.
// History is arranged the way the work item store records it: revision 1 is the creation (long ago, in no sprint), and a
// sprint, state or points change is a later revision timed at noon UTC on the day it happens, so "from that day on" holds.
public sealed class PostgresSprintMetricsContractTests : SprintMetricsContract
{
    private static readonly DateTimeOffset Created = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private PostgresWorkItemStoreArrangement _arrangement = null!;
    private NpgsqlDataSource _dataSource = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase()
    {
        DapperTypeHandlers.Register();
        _arrangement = await PostgresWorkItemStoreArrangement.CreateAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_arrangement.Database.ConnectionString));
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _arrangement.DisposeAsync();
    }

    protected override ISprintMetrics CreateMetrics() => new SprintMetrics(_dataSource, TimeProvider.System);

    protected override async Task<Guid> ArrangeProjectAsync() => (await _arrangement.ArrangeProjectAsync()).ProjectId;

    protected override async Task<Guid> ArrangeSprintAsync(Guid projectId, string name, DateOnly start, DateOnly end)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @projectId, @name, @start, @end)",
            new { id, projectId, name, start, end });
        return id;
    }

    protected override async Task<Guid> ArrangeItemAsync(Guid projectId, WorkItemTypeId type, decimal? points, Guid? sprintId, bool addedAfterStart, DateOnly? completedOn)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        var builder = new WorkItemBuilder(projectId).WithType(type.ToString().ToLowerInvariant());
        if (points is { } value)
        {
            builder.WithPoints(value);
        }

        var id = await builder.InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @created WHERE work_item_id = @id AND rev = 1", new { id, created = Created });
        if (sprintId is { } sprint)
        {
            var start = await connection.ExecuteScalarAsync<DateOnly>("SELECT start_date FROM sprint WHERE id = @sprint", new { sprint });
            await MoveAsync(connection, id, sprint, addedAfterStart ? start : start.AddDays(-1));
        }

        if (completedOn is { } day)
        {
            await CompleteAsync(connection, id, day);
        }

        return id;
    }

    protected override async Task MoveItemAsync(Guid itemId, Guid? toSprintId, DateOnly on)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await MoveAsync(connection, itemId, toSprintId, on);
    }

    protected override async Task CompleteItemAsync(Guid itemId, DateOnly on)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await CompleteAsync(connection, itemId, on);
    }

    protected override async Task ReopenItemAsync(Guid itemId, DateOnly on)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await EnterAsync(connection, itemId, on, "inProgress");
    }

    protected override async Task RemoveItemAsync(Guid itemId, DateOnly on)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await EnterAsync(connection, itemId, on, "removed");
    }

    protected override async Task SetTeamCapacityAsync(Guid sprintId, decimal points)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        var user = await new AppUserBuilder().InsertAsync(connection);
        await connection.ExecuteAsync(
            "INSERT INTO sprint_capacity (id, sprint_id, user_id, capacity_points) VALUES (@id, @sprintId, @user, @points)",
            new { id = Guid.NewGuid(), sprintId, user, points });
    }

    protected override async Task SetPointsAsync(Guid itemId, decimal? points)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await AppendAsync(connection, itemId, TimeProvider.System.GetUtcNow(), FieldIds.Points, "null", points is null ? "null" : points.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await connection.ExecuteAsync("UPDATE work_item SET story_points = @points WHERE id = @itemId", new { itemId, points });
    }

    protected override async Task SetVelocityOverrideAsync(Guid projectId, decimal? value)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await connection.ExecuteAsync("UPDATE project SET velocity_override = @value WHERE id = @projectId", new { projectId, value });
    }

    private static async Task MoveAsync(NpgsqlConnection connection, Guid itemId, Guid? toSprintId, DateOnly on)
    {
        var from = await connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @itemId", new { itemId });
        await AppendAsync(connection, itemId, Noon(on), FieldIds.Sprint, Json(from), Json(toSprintId));
        await connection.ExecuteAsync("UPDATE work_item SET sprint_id = @toSprintId WHERE id = @itemId", new { itemId, toSprintId });
    }

    private static Task CompleteAsync(NpgsqlConnection connection, Guid itemId, DateOnly on) => EnterAsync(connection, itemId, on, "completed");

    private static async Task EnterAsync(NpgsqlConnection connection, Guid itemId, DateOnly on, string category)
    {
        var item = await connection.QuerySingleAsync<(Guid State, Guid Target)>(
            """
            SELECT w.state_id, (SELECT s.id FROM state s WHERE s.project_id = w.project_id AND s.type = w.type AND s.category = @category AND s.archived_at IS NULL ORDER BY s.sort_order LIMIT 1) AS target
            FROM work_item w WHERE w.id = @itemId
            """,
            new { itemId, category });
        await AppendAsync(connection, itemId, Noon(on), FieldIds.State, Json(item.State), Json(item.Target));
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state WHERE id = @itemId", new { itemId, state = item.Target });
    }

    private static async Task AppendAsync(NpgsqlConnection connection, Guid itemId, DateTimeOffset at, string field, string from, string to)
    {
        var rev = await connection.ExecuteScalarAsync<int>("SELECT max(rev) + 1 FROM revision WHERE work_item_id = @itemId", new { itemId });
        await new RevisionBuilder(itemId).WithRev(rev).WithChangedAt(at)
            .WithChanges($$"""[{"field":"{{field}}","from":{{from}},"to":{{to}},"fromLabel":null,"toLabel":null}]""")
            .InsertAsync(connection);
    }

    private static DateTimeOffset Noon(DateOnly day) => new(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero);

    private static string Json(Guid? id) => id is null ? "null" : $"\"{id}\"";
}
