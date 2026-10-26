using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Import.Apply;

// The project's work items that an import wrote earlier, by Jira key. A run reads them once, so an issue applied alone (a retry) or before its
// parent still finds the parent an earlier run wrote, instead of clearing it.
public sealed class EarlierItems(NpgsqlDataSource dataSource)
{
    public async Task LoadAsync(ImportRun run, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string Key, Guid Id)>(new CommandDefinition(
            "SELECT original_system_id AS Key, id AS Id FROM work_item WHERE project_id = @projectId AND original_system = 'jira' AND original_system_id IS NOT NULL",
            new { projectId = run.ProjectId }, cancellationToken: cancellationToken));
        foreach (var (key, id) in rows)
        {
            run.EarlierItemIds[key] = id;
        }
    }
}
