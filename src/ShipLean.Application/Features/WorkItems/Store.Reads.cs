using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore
{
    public async Task<WorkItemSnapshot> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await WorkItemQueries.RowsAsync(connection, null, $"{WorkItemRow.Select} WHERE w.id = @id", new { id }, cancellationToken);
        var row = rows.SingleOrDefault() ?? throw ApiException.NotFound("Work item not found.");
        var links = await WorkItemQueries.LinksAsync(connection, null, [id], cancellationToken);
        return row.ToSnapshot(links.GetValueOrDefault(id) ?? []);
    }

    public async Task<IReadOnlyList<RevisionSnapshot>> GetRevisionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var exists = await connection.QuerySingleAsync<bool>(new CommandDefinition("SELECT EXISTS (SELECT 1 FROM work_item WHERE id = @id)", new { id }, cancellationToken: cancellationToken));
        return exists ? await WorkItemQueries.RevisionRowsAsync(connection, null, id, cancellationToken) : throw ApiException.NotFound("Work item not found.");
    }
}
