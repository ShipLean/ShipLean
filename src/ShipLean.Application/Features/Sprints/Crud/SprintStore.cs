using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.Sprints;

// ISprintStore over Postgres. A create or update that owns its transaction takes a per-project advisory lock first, so two
// writers can't both pass the overlap check. Events go out after the store's own commit.
// A create that joins the caller's transaction takes no lock and publishes no event: holding a lock in a transaction we
// don't commit would block every other writer until the caller ends it, and only the caller knows when it commits. So a
// joined create is only for a project nobody else can see yet (a new project's first sprints). A caller writing into an
// existing project, such as the Jira import, must not join: it would run the overlap and name checks unserialised against
// a concurrent create. It must call without a transaction, or take the project lock itself first.
// Never call IWorkItemStore while holding this lock or a sprint row lock (the work item store locks the project first).
public sealed partial class SprintStore(NpgsqlDataSource dataSource, TimeProvider clock, IProjectEvents events, IWorkItemStore workItems) : ISprintStore
{
    private static readonly string SelectSprint = $"SELECT {SprintRow.Columns} FROM sprint WHERE id = @id AND archived_at IS NULL";
    private static readonly string SelectSprintForUpdate = SelectSprint + " FOR UPDATE";

    public async Task<SprintDto> CreateAsync(Guid projectId, SprintCreate create, DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        if (create is null)
        {
            throw ApiException.Validation("A sprint needs a name and dates.");
        }

        if (transaction is not null)
        {
            return await InsertAsync(transaction.Connection!, transaction, projectId, create, serialise: false, cancellationToken);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var own = await connection.BeginTransactionAsync(cancellationToken);
        var created = await InsertAsync(connection, own, projectId, create, serialise: true, cancellationToken);
        await own.CommitAsync(cancellationToken);
        await events.PublishAsync(new SprintChanged(projectId, created.Id));
        return created;
    }

    public async Task<SprintDto> UpdateAsync(Guid id, SprintUpdate update, CancellationToken cancellationToken = default)
    {
        if (update is null)
        {
            throw ApiException.Validation("A sprint update needs a body.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var sprint = await LoadAsync(connection, transaction, id, forUpdate: false, cancellationToken);
        await LockProjectAsync(connection, transaction, sprint.ProjectId, cancellationToken);
        sprint = await LoadAsync(connection, transaction, id, forUpdate: true, cancellationToken);
        var name = (update.Name ?? sprint.Name).Trim();
        var start = update.StartDate ?? sprint.Start;
        var end = update.EndDate ?? sprint.End;
        await CheckAsync(connection, transaction, sprint.ProjectId, id, name, start, end, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE sprint SET name = @name, start_date = @start, end_date = @end, goal = @goal, updated_at = @now WHERE id = @id",
            new { id, name, start, end, goal = update.Goal ?? sprint.Goal, now = clock.GetUtcNow() }, transaction, cancellationToken: cancellationToken));
        var saved = await LoadAsync(connection, transaction, id, forUpdate: false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(new SprintChanged(saved.ProjectId, id));
        return saved.ToDto(SprintLabels.Today(clock));
    }

    private async Task<SprintDto> InsertAsync(DbConnection connection, DbTransaction transaction, Guid projectId, SprintCreate create, bool serialise, CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM project WHERE id = @projectId)", new { projectId }, transaction, cancellationToken: cancellationToken));
        if (!exists)
        {
            throw ApiException.NotFound("There is no such project.");
        }

        if (serialise)
        {
            await LockProjectAsync(connection, transaction, projectId, cancellationToken);
        }

        var name = create.Name?.Trim() ?? string.Empty;
        await CheckAsync(connection, transaction, projectId, null, name, create.StartDate, create.EndDate, cancellationToken);
        var id = Guid.NewGuid();
        var now = clock.GetUtcNow();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO sprint (id, project_id, name, start_date, end_date, goal, created_at, updated_at)
            VALUES (@id, @projectId, @name, @start, @end, @goal, @now, @now)
            """,
            new { id, projectId, name, start = create.StartDate, end = create.EndDate, goal = create.Goal ?? string.Empty, now }, transaction, cancellationToken: cancellationToken));
        return (await LoadAsync(connection, transaction, id, forUpdate: false, cancellationToken)).ToDto(SprintLabels.Today(clock));
    }

    private static Task LockProjectAsync(DbConnection connection, DbTransaction transaction, Guid projectId, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('sprints:' || @projectId::text, 0))", new { projectId }, transaction, cancellationToken: cancellationToken));

    private static async Task CheckAsync(
        DbConnection connection, DbTransaction transaction, Guid projectId, Guid? self, string name, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || start == default || end == default || end < start)
        {
            throw ApiException.Validation("A sprint needs a name and dates, with an end on or after its start.");
        }

        var overlaps = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM sprint WHERE project_id = @projectId AND archived_at IS NULL AND id IS DISTINCT FROM @self AND start_date <= @end AND @start <= end_date)",
            new { projectId, self, start, end }, transaction, cancellationToken: cancellationToken));
        if (overlaps)
        {
            throw ApiException.Validation("Sprints cannot overlap.");
        }

        var taken = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM sprint WHERE project_id = @projectId AND archived_at IS NULL AND id IS DISTINCT FROM @self AND lower(name) = lower(@name))",
            new { projectId, self, name }, transaction, cancellationToken: cancellationToken));
        if (taken)
        {
            throw ApiException.Conflict("A sprint with that name exists.");
        }
    }

    private static async Task<SprintRow> LoadAsync(DbConnection connection, DbTransaction? transaction, Guid id, bool forUpdate, CancellationToken cancellationToken)
        => await connection.QuerySingleOrDefaultAsync<SprintRow>(new CommandDefinition(
            forUpdate ? SelectSprintForUpdate : SelectSprint, new { id }, transaction, cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("There is no such sprint.");
}
