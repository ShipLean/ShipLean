using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.States;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.LocalEnvironment.Database;
using Shouldly;

namespace ShipLean.WebApi.Tests.IssueIntake.Settings;

// StateAdd.IsIntake reaches state.is_intake (D7), and a project copy keeps it; a state added without it is not intake.
[TestFixture]
public sealed class IntakeStateTests
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

    [Test]
    public async Task StateCatalog_WhenAddIntake_ShouldPersistFlag()
    {
        var project = await ArrangeProjectAsync();
        var triage = Guid.NewGuid();
        var triaged = Guid.NewGuid();

        await ApplyAsync(project, new StateAdd(triage, WorkItemTypeId.Story, "Triage", StateCategory.Proposed, IsIntake: true), new StateAdd(triaged, WorkItemTypeId.Story, "Triaged", StateCategory.Proposed));

        (await IsIntakeAsync(triage)).ShouldBeTrue();
        (await IsIntakeAsync(triaged)).ShouldBeFalse();
    }

    [Test]
    public async Task StateCatalog_WhenCopyIntake_ShouldKeepFlag()
    {
        var from = await ArrangeProjectAsync();
        await ApplyAsync(from, new StateAdd(Guid.NewGuid(), WorkItemTypeId.Bug, "Triage", StateCategory.Proposed, IsIntake: true));
        var to = await ArrangeProjectAsync(withDefaults: false);

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await new StateCatalog(_dataSource).CopyAsync(from, to, transaction);
        await transaction.CommitAsync();

        var copied = await connection.QueryAsync<(string Name, bool IsIntake)>("SELECT name, is_intake FROM state WHERE project_id = @to AND type = 'bug'", new { to });
        copied.Where(s => s.IsIntake).Select(s => s.Name).ShouldBe(["Triage"]);
    }

    private async Task ApplyAsync(Guid project, params StateAdd[] adds)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await new StateCatalog(_dataSource).ApplyAsync(project, new StateChanges(adds, [], [], []), transaction);
        await transaction.CommitAsync();
    }

    private async Task<bool> IsIntakeAsync(Guid stateId)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<bool>("SELECT is_intake FROM state WHERE id = @stateId", new { stateId });
    }

    private async Task<Guid> ArrangeProjectAsync(bool withDefaults = true)
    {
        var id = Guid.NewGuid();
        var key = new string([.. Enumerable.Range(0, 4).Select(_ => (char)('A' + Random.Shared.Next(26)))]);
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("INSERT INTO project (id, key, name) VALUES (@id, @key, 'Intake project')", new { id, key });
        if (withDefaults)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await new StateCatalog(_dataSource).CreateDefaultsAsync(id, transaction);
            await transaction.CommitAsync();
        }

        return id;
    }
}
