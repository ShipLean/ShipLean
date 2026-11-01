using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.WorkItem;

// The retros of one work item, newest first: its own agent retros and, for a feature, the conductor's compiled one. Retros are
// keyed by retrospective.work_item_id, so a task (which nobody writes a retro for) answers an empty list. Archived retros stay out.
public sealed class WorkItemRetroReader(NpgsqlDataSource dataSource, IRetroCards retroCards) : IWorkItemRetroReader
{
    public async Task<WorkItemRetrosDto> GetAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var retroIds = (await connection.QueryAsync<Guid>(
            new CommandDefinition(
                "SELECT id FROM retrospective WHERE work_item_id = @workItemId AND archived_at IS NULL ORDER BY created_at DESC, id DESC",
                new { workItemId },
                cancellationToken: cancellationToken))).ToList();
        var cards = await retroCards.LoadAsync(retroIds, cancellationToken);
        return new WorkItemRetrosDto([.. cards]);
    }
}
