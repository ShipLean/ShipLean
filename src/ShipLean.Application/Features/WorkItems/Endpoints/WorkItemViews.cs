using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems.Endpoints;

// What the work item read endpoints do: the access check first, then the read model (and the project's tags).
[RegisterPerRequest]
public sealed class WorkItemViews(IProjectAccess access, WorkItemReads reads, NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<WorkItemDto>> ListAsync(Guid projectId, WorkItemTypeId? type, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        return await reads.ListAsync(projectId, type, cancellationToken);
    }

    public async Task<WorkItemDto> ByKeyAsync(Guid projectId, string itemKey, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        return await reads.GetByKeyAsync(projectId, itemKey, cancellationToken);
    }

    public async Task<WorkItemDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, id, ProjectRole.Member, cancellationToken);
        return await reads.GetAsync(id, cancellationToken);
    }

    // Distinct across the project's items outside the Removed category, ordered ignoring case.
    public async Task<IReadOnlyList<string>> TagsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var tags = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT tag FROM (
                SELECT DISTINCT t.tag
                FROM work_item w
                JOIN state s ON s.id = w.state_id
                CROSS JOIN LATERAL unnest(w.tags) AS t(tag)
                WHERE w.project_id = @projectId AND s.category <> 'removed'
            ) distinct_tags
            ORDER BY lower(tag) COLLATE "C", tag COLLATE "C"
            """,
            new { projectId },
            cancellationToken: cancellationToken));
        return [.. tags];
    }
}
