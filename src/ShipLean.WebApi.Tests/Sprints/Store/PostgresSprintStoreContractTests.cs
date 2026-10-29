using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Fakes.Holidays;
using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Application.Features.Sprints;
using ShipLean.Application.Features.Sprints.Metrics;
using ShipLean.Contracts;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Sprints;
using ShipLean.WebApi.Tests.Boards.Settings;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.Sprints.Store;

// The sprint store contract on Postgres: one seeded clone for the class, a new project for every arranged project, and a
// real work item store with pass-through rules behind the rollover.
public sealed class PostgresSprintStoreContractTests : SprintStoreContract
{
    private readonly List<DbConnection> _held = [];
    private PostgresWorkItemStoreArrangement _arrangement = null!;
    private NpgsqlDataSource _dataSource = null!;
    private readonly InMemoryPublicHolidays _holidays = new();

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

    [TearDown]
    public async Task EndTransactions()
    {
        foreach (var connection in _held)
        {
            await connection.DisposeAsync();
        }

        _held.Clear();
    }

    protected override ISprintStore CreateStore()
        => new SprintStore(
            _dataSource, TimeProvider.System, new DiscardingProjectEvents(), _arrangement.CreateStore(new PassThroughWorkItemRules(), ShipLean.TestSupport.SeedIds.Users.Joel),
            _holidays, new SprintMetrics(_dataSource, TimeProvider.System), CreateRecompute());

    protected override ISprintCapacityRecompute CreateRecompute() => new SprintCapacityRecompute(_dataSource, TimeProvider.System, _holidays);

    protected override async Task ArrangeHolidaysAsync(Guid projectId, string country, params (DateOnly Date, string Name)[] holidays)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO holiday_country (code, name) VALUES (@country, @country) ON CONFLICT (code) DO NOTHING", new { country });
        await connection.ExecuteAsync("UPDATE project SET holiday_country = @country WHERE id = @projectId", new { country, projectId });
        _holidays.AddCountry(country, country);
        foreach (var (date, name) in holidays)
        {
            _holidays.AddHoliday(country, date, name);
        }
    }

    protected override async Task ArrangeWorkingDaysAsync(Guid projectId, params int[] isoDays)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await connection.ExecuteAsync("UPDATE project SET working_days = @isoDays::smallint[] WHERE id = @projectId", new { isoDays, projectId });
    }

    protected override async Task ArrangeStoredCapacityAsync(Guid sprintId, decimal? velocity, params (Guid UserId, decimal? Days, decimal Points)[] rows)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        await connection.ExecuteAsync("UPDATE sprint SET capacity_velocity = @velocity WHERE id = @sprintId", new { velocity, sprintId });
        foreach (var (userId, days, points) in rows)
        {
            await connection.ExecuteAsync(
                "INSERT INTO sprint_capacity (id, sprint_id, user_id, capacity_points, days) VALUES (@id, @sprintId, @userId, @points, @days)",
                new { id = Guid.NewGuid(), sprintId, userId, points, days });
        }
    }

    protected override async Task<Guid> ArrangeProjectAsync() => (await _arrangement.ArrangeProjectAsync()).ProjectId;

    protected override async Task<Guid> ArrangeMemberAsync(Guid projectId)
    {
        await using var connection = await _arrangement.Database.OpenAsync();
        var user = await new AppUserBuilder().InsertAsync(connection);
        await connection.ExecuteAsync("INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @projectId, @user, 'member')", new { id = Guid.NewGuid(), projectId, user });
        return user;
    }

    protected override async Task<DbTransaction> BeginTransactionAsync()
    {
        var connection = await _dataSource.OpenConnectionAsync();
        _held.Add(connection);
        return await connection.BeginTransactionAsync();
    }
}
