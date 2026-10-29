using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.States;

// IStateCatalog over Postgres. Writes join the caller's transaction and publish nothing: the caller commits and publishes
// statesChanged. Archived states (deleted in settings) are invisible to every read.
public sealed partial class StateCatalog(NpgsqlDataSource dataSource) : IStateCatalog
{
    private static readonly (string Name, StateCategory Category)[] Defaults =
    [
        ("New", StateCategory.Proposed), ("Active", StateCategory.InProgress), ("Resolved", StateCategory.Resolved),
        ("Closed", StateCategory.Completed), ("Removed", StateCategory.Removed),
    ];

    public async Task<IReadOnlyList<StateDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireProjectAsync(connection, null, projectId, cancellationToken);
        var rows = await connection.QueryAsync<StateRow>(new CommandDefinition(
            $"""
            SELECT s.id, s.type, s.name, s.category, s.sort_order AS sortOrder, (SELECT count(*) FROM work_item w WHERE w.state_id = s.id)::int AS itemCount
            FROM state s
            WHERE s.project_id = @projectId AND s.archived_at IS NULL
            ORDER BY CASE s.type WHEN 'epic' THEN 1 WHEN 'feature' THEN 2 WHEN 'story' THEN 3 WHEN 'bug' THEN 4 ELSE 5 END, s.sort_order, s.name
            """,
            new { projectId }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToDto())];
    }

    public async Task CreateDefaultsAsync(Guid projectId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection!;
        await RequireProjectAsync(connection, transaction, projectId, cancellationToken);
        var any = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM state WHERE project_id = @projectId AND archived_at IS NULL)", new { projectId }, transaction, cancellationToken: cancellationToken));
        if (any)
        {
            return;
        }

        foreach (var type in Enum.GetValues<WorkItemTypeId>())
        {
            for (var i = 0; i < Defaults.Length; i++)
            {
                await InsertAsync(connection, transaction, Guid.CreateVersion7(), projectId, type, Defaults[i].Name, Defaults[i].Category, i + 1, cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> CopyAsync(Guid fromProjectId, Guid toProjectId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection!;
        await RequireProjectAsync(connection, transaction, fromProjectId, cancellationToken);
        await RequireProjectAsync(connection, transaction, toProjectId, cancellationToken);
        var source = await connection.QueryAsync<StateRow>(new CommandDefinition(
            $"SELECT {StateRow.Columns} FROM state s WHERE s.project_id = @fromProjectId AND s.archived_at IS NULL ORDER BY s.type, s.sort_order",
            new { fromProjectId }, transaction, cancellationToken: cancellationToken));
        var map = new Dictionary<Guid, Guid>();
        foreach (var state in source)
        {
            var id = Guid.CreateVersion7();
            await InsertAsync(connection, transaction, id, toProjectId, state.TypeId, state.Name, state.CategoryId, state.SortOrder, cancellationToken);
            map[state.Id] = id;
        }

        return map;
    }

    private static async Task RequireProjectAsync(DbConnection connection, DbTransaction? transaction, Guid projectId, CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM project WHERE id = @projectId)", new { projectId }, transaction, cancellationToken: cancellationToken));
        if (!exists)
        {
            throw ApiException.NotFound("There is no such project.");
        }
    }

    private static Task InsertAsync(
        DbConnection connection, DbTransaction transaction, Guid id, Guid projectId, WorkItemTypeId type, string name, StateCategory category, int order, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO state (id, project_id, type, name, category, sort_order) VALUES (@id, @projectId, @type, @name, @category, @order)",
            new { id, projectId, type = StateRow.TypeText(type), name, category = StateRow.CategoryText(category), order }, transaction, cancellationToken: cancellationToken));
}
