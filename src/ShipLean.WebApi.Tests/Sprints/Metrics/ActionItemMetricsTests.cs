using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Sprints.Metrics;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport.Builders;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.Sprints.Metrics;

// An action item counts like a story in committed, completed, burndown and velocity (an ended sprint with one action item and no story).
[TestFixture]
public class ActionItemMetricsTests
{
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

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    private SprintMetrics Metrics() => new(_dataSource, TimeProvider.System);

    // An ended 14-day sprint holding one 5-point action item, completed (or not) four days before the sprint's end.
    private async Task<(Guid Project, Guid Sprint, DateOnly End)> ArrangeAsync(bool completed)
    {
        var project = (await _arrangement.ArrangeProjectAsync()).ProjectId;
        await using var connection = await _arrangement.Database.OpenAsync();
        var end = Today.AddDays(-1);
        var start = end.AddDays(-13);
        var sprint = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@sprint, @project, 'Action sprint', @start, @end)", new { sprint, project, start, end });
        var item = await new WorkItemBuilder(project).WithType("action").WithPoints(5).InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @created WHERE work_item_id = @item AND rev = 1", new { item, created = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero) });
        await Append(connection, item, 2, Noon(start.AddDays(-1)), FieldIds.Sprint, "null", $"\"{sprint}\"");
        await connection.ExecuteAsync("UPDATE work_item SET sprint_id = @sprint WHERE id = @item", new { item, sprint });
        if (completed)
        {
            var states = await connection.QuerySingleAsync<(Guid From, Guid To)>(
                """
                SELECT w.state_id, (SELECT s.id FROM state s WHERE s.project_id = w.project_id AND s.type = w.type AND s.category = 'completed' AND s.archived_at IS NULL ORDER BY s.sort_order LIMIT 1)
                FROM work_item w WHERE w.id = @item
                """,
                new { item });
            await Append(connection, item, 3, Noon(end.AddDays(-4)), FieldIds.State, $"\"{states.From}\"", $"\"{states.To}\"");
            await connection.ExecuteAsync("UPDATE work_item SET state_id = @to WHERE id = @item", new { item, states.To });
        }

        return (project, sprint, end);
    }

    private static async Task Append(NpgsqlConnection connection, Guid item, int rev, DateTimeOffset at, string field, string from, string to)
        => await new RevisionBuilder(item).WithRev(rev).WithChangedAt(at)
            .WithChanges($$"""[{"field":"{{field}}","from":{{from}},"to":{{to}},"fromLabel":null,"toLabel":null}]""")
            .InsertAsync(connection);

    private static DateTimeOffset Noon(DateOnly day) => new(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Figures_WhenActionItemInSprint_ShouldCountItsPoints()
    {
        var (_, sprint, _) = await ArrangeAsync(completed: true);

        var metrics = await Metrics().GetSprintAsync(sprint);

        (metrics.Committed, metrics.Completed).ShouldBe((5m, 5m));
    }

    [Test]
    public async Task Burndown_WhenActionItemClosed_ShouldBurnItsPoints()
    {
        var (_, sprint, end) = await ArrangeAsync(completed: true);

        var burndown = (await Metrics().GetSprintAsync(sprint)).Burndown;

        burndown[0].Remaining.ShouldBe(5m);
        burndown.Where(p => p.Date >= end.AddDays(-4)).ShouldAllBe(p => p.Remaining == 0m);
        burndown.Where(p => p.Date < end.AddDays(-4)).ShouldAllBe(p => p.Remaining == 5m);
    }

    [Test]
    public async Task Velocity_WhenActionItemCompleted_ShouldCount()
    {
        var (project, sprint, _) = await ArrangeAsync(completed: true);

        var velocity = await Metrics().GetVelocityAsync(project);

        velocity.Sprints.Select(s => (s.SprintId, s.Committed, s.Completed)).ShouldBe([(sprint, 5m, 5m)]);
        velocity.Average.ShouldBe(5m);
    }

    [Test]
    public async Task Forecast_WhenActionItemInBacklog_ShouldTakeItsPoints()
    {
        var project = (await _arrangement.ArrangeProjectAsync()).ProjectId;
        await using var connection = await _arrangement.Database.OpenAsync();
        var item = await new WorkItemBuilder(project).WithType("action").WithPoints(5).InsertAsync(connection);

        var forecast = await Metrics().GetForecastAsync(project);

        var taken = forecast.Sprints.Single(sprint => sprint.ItemCount > 0);
        (taken.LastItemId, taken.ItemCount, taken.Points).ShouldBe((item, 1, 5m));
    }
}
